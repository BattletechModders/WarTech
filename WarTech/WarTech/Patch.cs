using BattleTech;
using BattleTech.Framework;
using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using BattleTech.UI;
using Harmony;
using HBS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WarTech {

    [HarmonyPatch(typeof(SimGameState), "AddPredefinedContract")]
    public static class SimGameState_AddPredefinedContract_Patch {
        static void Postfix(SimGameState __instance, Contract __result) {
            try {
                if (Fields.warmission) {
                    __result.SetInitialReward(Mathf.RoundToInt(__result.InitialContractValue * Fields.settings.priorityContactPayPercentage));
                    __result.Override.salvagePotential = Mathf.RoundToInt(__result.Override.salvagePotential * Fields.settings.priorityContactPayPercentage);
                    ReflectionHelper.InvokePrivateMethode(__result, "set_SalvagePotential", new object[] { __result.Override.salvagePotential });
                    Fields.warmission = false;
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(SimGameState), "PrepareBreadcrumb")]
    public static class SimGameState_PrepareBreadcrumb_Patch {
        static void Postfix(SimGameState __instance, ref Contract contract) {
            try {
                if (contract.IsPriorityContract) {
                    Fields.warmission = true;
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(StarSystem), "GenerateInitialContracts")]
    public static class StarSystem_GenerateInitialContracts_Patch {
        static void Postfix(StarSystem __instance) {
            try {
                if (Fields.settings.debug) {
                    ReflectionHelper.InvokePrivateMethode(__instance.Sim, "SetReputation", new object[] { Faction.Steiner, 100, StatCollection.StatOperation.Set, null });
                }
                __instance.Sim.GlobalContracts.Clear();
                foreach (KeyValuePair<Faction, FactionDef> pair in __instance.Sim.FactionsDict) {
                    if (!Helper.IsExcluded(pair.Key) && Helper.IsAtWar(pair.Key)) {
                        SimGameReputation rep = __instance.Sim.GetReputation(pair.Key);
                        int numberOfContracts;
                        switch (rep) {
                            case SimGameReputation.LIKED: {
                                    numberOfContracts = 1;
                                    break;
                                }
                            case SimGameReputation.FRIENDLY: {
                                    numberOfContracts = 2;
                                    break;
                                }
                            case SimGameReputation.ALLIED: {
                                    numberOfContracts = 3;
                                    break;
                                }
                            default: {
                                    numberOfContracts = 0;
                                    break;
                                }
                        }
                        List<TargetSystem> targets = Fields.availableTargets[pair.Key].OrderByDescending(x => Helper.GetOffenceValue(x.system) + Helper.GetDefenceValue(x.system) + x.factionNeighbours[pair.Key]).ToList();//
                        numberOfContracts = Mathf.Min(numberOfContracts, targets.Count);
                        for (int i = 0; i < numberOfContracts; i++) {
                            Contract contract = Helper.GetNewWarContract(__instance.Sim, targets[i].system.Def.Difficulty, pair.Key, targets[i].system.Owner, targets[i].system);
                            contract.Override.contractDisplayStyle = ContractDisplayStyle.BaseCampaignStory;
                            contract.SetInitialReward(Mathf.RoundToInt(contract.InitialContractValue * Fields.settings.priorityContactPayPercentage));
                            contract.Override.salvagePotential = Mathf.RoundToInt(contract.Override.salvagePotential * Fields.settings.priorityContactPayPercentage);
                            contract.Override.negotiatedSalvage = 1f;
                            __instance.Sim.GlobalContracts.Add(contract);
                        }

                    }
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(Contract), "CompleteContract")]
    public static class Contract_CompleteContract_Patch {

        static void Postfix(Contract __instance, MissionResult result) {
            try {
                GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                StarSystem system = game.Simulation.StarSystems.Find(x => x.ID == __instance.TargetSystem);
                Faction oldOwner = system.Def.Owner;
                if (result == MissionResult.Victory) {
                    system = Helper.PlayerAttackSystem(system, game.Simulation, __instance.Override.employerTeam.faction, __instance.Override.targetTeam.faction, true);
                }
                else {
                    system = Helper.PlayerAttackSystem(system, game.Simulation, __instance.Override.employerTeam.faction, __instance.Override.targetTeam.faction, false);
                }
                if (system.Def.Owner != oldOwner) {
                    SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(game.Simulation);
                    interruptQueue.QueueGenericPopup_NonImmediate("Conquered", Helper.GetFactionName(system.Def.Owner, game.Simulation.DataManager) + " took " + system.Name + " from " + Helper.GetFactionName(oldOwner, game.Simulation.DataManager), true, null);
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(GameInstanceSave))]
    [HarmonyPatch(new Type[] { typeof(GameInstance), typeof(SaveReason) })]
    public static class GameInstanceSave_Constructor_Patch {
        static void Postfix(GameInstanceSave __instance, GameInstance gameInstance, SaveReason saveReason) {
            try {
                Helper.SaveState(__instance.InstanceGUID, __instance.SaveTime);
            }
            catch (Exception e) {
                Logger.LogError(e);
            }

        }
    }

    [HarmonyPatch(typeof(GameInstance), "Load")]
    public static class GameInstance_Load_Patch {
        static void Prefix(GameInstanceSave save) {
            try {
                Helper.LoadState(save.InstanceGUID, save.SaveTime);
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(Starmap), "PopulateMap", new Type[] { typeof(SimGameState) })]
    public static class Starmap_PopulateMap_Patch {

        static void Postfix(SimGameState simGame) {
            try {
                if (Fields.stateOfWar == null) {
                    //first init
                    Fields.stateOfWar = new List<PlanetControlState>();
                    foreach (StarSystem system in simGame.StarSystems) {
                        List<FactionControl> factionList = new List<FactionControl>();
                        factionList.Add(new FactionControl(100, system.Owner));
                        Fields.stateOfWar.Add(new PlanetControlState(factionList, system.Name, system.Owner));
                    }
                }

                foreach (StarSystem system in simGame.StarSystems) {
                    bool battle = false;
                    FactionControl factionControl = null;
                    if (Fields.stateOfWar != null) {
                        foreach (PlanetControlState control in Fields.stateOfWar) {
                            if (control.system.Equals(system.Name)) {
                                if (system.Owner != control.owner) {
                                    battle = true;
                                    factionControl = control.factionList.Find(x => x.faction == control.owner);
                                    break;
                                }
                            }
                        }
                    }
                    StarSystem system2 = simGame.StarSystems.Find(x => x.Name.Equals(system.Name));
                    system2 = Helper.ChangeOwner(system, factionControl, simGame, battle);
                    system2 = Helper.ChangeWarDescription(system2, simGame);
                }

                if (Fields.currentEnemies == null) {
                    Helper.RefreshEnemies(simGame);
                    Dictionary<Faction, FactionDef> factions = (Dictionary<Faction, FactionDef>)AccessTools.Field(typeof(SimGameState), "factions").GetValue(simGame);
                    foreach (KeyValuePair<Faction, FactionDef> pair in factions) {
                        if (Fields.currentEnemies.ContainsKey(pair.Key)) {
                            Faction[] enemies = Fields.currentEnemies[pair.Key].ToArray();
                            ReflectionHelper.InvokePrivateMethode(pair.Value, "set_Enemies", new object[] { enemies });
                        }
                    }
                }
                Helper.RefreshTargets(simGame);
                if (Fields.factionResources.Count == 0) {
                    Helper.RefreshResources(simGame);
                }
                if (Fields.WarFatique == null || Fields.WarFatique.Count == 0) {
                    Fields.WarFatique = new Dictionary<Faction, float>();
                    Dictionary<Faction, FactionDef> factions = (Dictionary<Faction, FactionDef>)AccessTools.Field(typeof(SimGameState), "factions").GetValue(simGame);
                    foreach (KeyValuePair<Faction, FactionDef> pair in factions) {
                        Fields.WarFatique.Add(pair.Key, 0);
                    }
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class SimGameState_OnDayPassed_Patch {
        static void Prefix(SimGameState __instance, int timeLapse) {
            try {
                //DAILY
                System.Random rand = new System.Random();
                foreach (KeyValuePair<Faction, FactionDef> pair in __instance.FactionsDict) {
                    if (!Helper.IsExcluded(pair.Key) && Helper.IsAtWar(pair.Key)) {
                        List<TargetSystem> availableTargets = Fields.availableTargets[pair.Key];
                        if (availableTargets.Count > 0) {
                            List<TargetSystem> targets = availableTargets.OrderByDescending(x => Helper.GetOffenceValue(x.system) + Helper.GetDefenceValue(x.system) + x.factionNeighbours[pair.Key]).ToList();
                            int numberOfAttacks = Mathf.Min(targets.Count, Mathf.CeilToInt(Fields.factionResources.Find(x => x.faction == pair.Key).offence / 100f));
                            for (int i = 0; i < numberOfAttacks; i++) {
                                StarSystem system = __instance.StarSystems.Find(x => x.Name == targets[i].system.Name);
                                if (system != null) {
                                    system = Helper.AttackSystem(system, __instance, pair.Key, rand);
                                }
                            }
                        }
                    }
                    else if (!Helper.IsExcluded(pair.Key) && Fields.WarFatique[pair.Key] > 0) {
                        Fields.WarFatique[pair.Key] -= Fields.settings.FatiqueRecoveredPerDay;
                    }
                }


                //MONTHLY
                int num = (timeLapse <= 0) ? 1 : timeLapse;
                if ((__instance.DayRemainingInQuarter - num <= 0)) {
                    foreach (KeyValuePair<string, string> changes in Fields.thisMonthChanges) {
                        StarSystem changedSystem = __instance.StarSystems.Find(x => x.Name.Equals(changes.Key));
                        if (!Helper.GetFactionName(changedSystem.Owner, __instance.DataManager).Equals(changes.Value)) {
                            War war = Helper.getWar(changedSystem.Owner);
                            war.monthlyEvents.Add(Helper.GetFactionName(changedSystem.Owner, __instance.DataManager) + " took " + changes.Key + " from " + changes.Value + ".");
                        }
                    }
                    Dictionary<Faction, FactionDef> factions = (Dictionary<Faction, FactionDef>)AccessTools.Field(typeof(SimGameState), "factions").GetValue(__instance);
                    foreach (KeyValuePair<Faction, FactionDef> pair in factions) {
                        List<Faction> fac = null;
                        if (Fields.neighbourFactions.ContainsKey(pair.Key)) {
                            fac = Fields.neighbourFactions[pair.Key];
                        }
                        if (!Helper.IsAtWar(pair.Key) && !Helper.IsExcluded(pair.Key) && fac != null && fac.Count > 0) {
                            if (rand.Next(0, 101) > Fields.WarFatique[pair.Key]) {
                                Faction enemy;
                                do {
                                    enemy = fac[rand.Next(fac.Count)];
                                } while (Helper.IsExcluded(enemy) || pair.Key == enemy);

                                bool joinedWar = false;
                                foreach (War war in Fields.currentWars) {
                                    if (war.attackers.Contains(enemy) && !Fields.removeWars.Contains(war.name)) {
                                        war.defenders.Add(pair.Key);
                                        war.monthlyEvents.Add(Helper.GetFactionName(pair.Key, __instance.DataManager) + " joined the war on the defending side.");
                                        joinedWar = true;
                                        break;
                                    }
                                    else if (war.defenders.Contains(enemy) && !Fields.removeWars.Contains(war.name)) {
                                        war.attackers.Add(pair.Key);
                                        war.monthlyEvents.Add(Helper.GetFactionName(pair.Key, __instance.DataManager) + " joined the war on the attacking side.");
                                        joinedWar = true;
                                        break;
                                    }
                                }
                                if (!joinedWar) {
                                    string Name = Helper.GetFactionShortName(pair.Key, __instance.DataManager) + " VS " + Helper.GetFactionShortName(enemy, __instance.DataManager);
                                    War war = new War(Name, new List<Faction>() { pair.Key }, new List<Faction>() { enemy });
                                    war.monthlyEvents.Add(Helper.GetFactionName(pair.Key, __instance.DataManager) + " declared war on " + Helper.GetFactionName(enemy, __instance.DataManager) + ".");
                                    Fields.currentWars.Add(war);
                                }
                            }
                        }
                        else if (Helper.IsAtWar(pair.Key)) {
                            Fields.WarFatique[pair.Key] += Fields.settings.FatiqueLostPerMonth;
                            if (rand.Next(0, 101) < Fields.WarFatique[pair.Key]) {
                                War war = Helper.getWar(pair.Key);
                                if (war.duration >= Fields.settings.minMonthDuration) {
                                    if (war.duration < Fields.settings.maxMonthDuration || Fields.settings.maxMonthDuration == -1) {
                                        if (!(Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.Count <= 0) && !(Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders.Count <= 0)) {
                                            if (Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.Contains(pair.Key)) {
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.Remove(pair.Key);
                                            }
                                            else {
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders.Remove(pair.Key);
                                            }
                                            Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add(Helper.GetFactionName(pair.Key, __instance.DataManager) + " left the war.");

                                            if (Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.Count <= 0) {
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("The attacking side lost the war.");
                                                if (!Fields.removeWars.Contains(war.name)) {
                                                    Fields.removeWars.Add(war.name);
                                                }
                                            }
                                            else if (Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders.Count <= 0) {
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("The Defending side lost the war.");
                                                if (!Fields.removeWars.Contains(war.name)) {
                                                    Fields.removeWars.Add(war.name);
                                                }
                                            }
                                        }
                                    }
                                    else {
                                        Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("The War ended Undecided.");
                                        if (!Fields.removeWars.Contains(war.name)) {
                                            Fields.removeWars.Add(war.name);
                                        }

                                    }
                                }
                            }
                        }
                    }
                    foreach (KeyValuePair<Faction, FactionDef> pair in factions) {
                        if (Fields.currentEnemies.ContainsKey(pair.Key)) {
                            Faction[] enemies = Fields.currentEnemies[pair.Key].ToArray();
                            ReflectionHelper.InvokePrivateMethode(pair.Value, "set_Enemies", new object[] { enemies });
                        }
                    }
                    SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(__instance);

                    foreach (War war in Fields.currentWars) {
                        war.duration += 1;
                        war.monthlyEvents.Add("\nAttackers:");
                        foreach (Faction fac in war.attackers) {
                            war.monthlyEvents.Add(Helper.GetFactionName(fac, __instance.DataManager) + " | Exhaustion: " + Fields.WarFatique[fac] + "%");
                        }
                        war.monthlyEvents.Add("\nDefenders:");
                        foreach (Faction fac in war.defenders) {
                            war.monthlyEvents.Add(Helper.GetFactionName(fac, __instance.DataManager) + " | Exhaustion: " + Fields.WarFatique[fac] + "%");
                        }
                        interruptQueue.QueueGenericPopup_NonImmediate(war.name, string.Join("\n", war.monthlyEvents.ToArray()) + "\n", true); war.monthlyEvents.Clear();
                    }
                    Fields.thisMonthChanges = new Dictionary<string, string>();
                    foreach (string war in Fields.removeWars) {
                        Fields.currentWars.Remove(Fields.currentWars.Find(x => x.name.Equals(war)));
                    }
                    Fields.removeWars.Clear();
                    Helper.RefreshResources(__instance);
                    Helper.RefreshEnemies(__instance);
                    Helper.RefreshTargets(__instance);
                    __instance.StopPlayMode();
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
                Logger.LogError(e);
            }
        }
    }
}