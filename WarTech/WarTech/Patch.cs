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

    [HarmonyPatch(typeof(StarSystem), "ResetContracts")]
    public static class StarSystem_ResetContracts_Patch {
        static void Postfix(StarSystem __instance) {
            try {
                AccessTools.Field(typeof(SimGameState), "globalContracts").SetValue(__instance.Sim, new List<Contract>());
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyBefore(new string[] { "de.morphyum.MercDeployments" })]
    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_Rehydrate_Patch {
        static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave) {
            try {
                foreach(Contract contract in __instance.GlobalContracts) {
                    contract.Override.contractDisplayStyle = ContractDisplayStyle.BaseCampaignStory;
                    int maxPriority = Mathf.FloorToInt(7 / __instance.Constants.Salvage.PrioritySalvageModifier);
                    contract.Override.salvagePotential = Mathf.Min(maxPriority, Mathf.RoundToInt(contract.SalvagePotential * Fields.settings.priorityContactPayPercentage));
                    contract.Override.negotiatedSalvage = 1f;
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }
    [HarmonyPatch(typeof(SimGameState), "AddPredefinedContract")]
    public static class SimGameState_AddPredefinedContract_Patch {
        static void Postfix(SimGameState __instance, Contract __result) {
            try {
                if (Fields.warmission) {
                    __result.SetInitialReward(Mathf.RoundToInt(__result.InitialContractValue * Fields.settings.priorityContactPayPercentage));
                    int maxPriority = Mathf.FloorToInt(7 / __instance.Constants.Salvage.PrioritySalvageModifier);
                    __result.Override.salvagePotential = Mathf.Min(maxPriority, Mathf.RoundToInt(__result.Override.salvagePotential * Fields.settings.priorityContactPayPercentage));
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
                            int maxPriority = Mathf.FloorToInt(7 / __instance.Sim.Constants.Salvage.PrioritySalvageModifier);
                            contract.Override.salvagePotential = Mathf.Min(maxPriority, Mathf.RoundToInt(contract.Override.salvagePotential * Fields.settings.priorityContactPayPercentage));
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
                    system2 = Helper.ChangeOwner(system, factionControl, simGame, battle, false);
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
                            List<TargetSystem> targets = availableTargets.OrderByDescending(x => Helper.GetOffenceValue(x.system) + Helper.GetDefenceValue(x.system) + Helper.GetNeighborValue(x.factionNeighbours, pair.Key)).ToList();
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
                            if (war != null) {
                                if (war.attackers.ContainsKey(changedSystem.Owner)) {
                                    war.monthlyEvents.Add("<color=" + Fields.settings.attackercolor + ">" + Helper.GetFactionName(changedSystem.Owner, __instance.DataManager) + "</color>" + " took " + "<color=" + Fields.settings.planetcolor + ">" + changes.Key + "</color>" + " from " + "<color=" + Fields.settings.defendercolor + ">" + changes.Value + "</color>" + ".");
                                }
                                else {
                                    war.monthlyEvents.Add("<color=" + Fields.settings.defendercolor + ">" + Helper.GetFactionName(changedSystem.Owner, __instance.DataManager) + "</color>" + " took " + "<color=" + Fields.settings.planetcolor + ">" + changes.Key + "</color>" + " from " + "<color=" + Fields.settings.attackercolor + ">" + changes.Value + "</color>" + ".");
                                }
                            }
                        }
                    }
                    foreach (War war in Fields.currentWars) {
                        war.monthlyEvents.Add("\n");
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
                                    if (war.attackers.ContainsKey(enemy) && !Fields.removeWars.Contains(war.name)) {
                                        war.defenders.Add(pair.Key, new WarProgression(new Dictionary<string, Faction>()));
                                        war.monthlyEvents.Add("<color=" + Fields.settings.defendercolor + ">" + Helper.GetFactionName(pair.Key, __instance.DataManager) + "</color>" + " joined the war.");
                                        joinedWar = true;
                                        break;
                                    }
                                    else if (war.defenders.ContainsKey(enemy) && !Fields.removeWars.Contains(war.name)) {
                                        war.attackers.Add(pair.Key, new WarProgression(new Dictionary<string, Faction>()));
                                        war.monthlyEvents.Add("<color=" + Fields.settings.attackercolor + ">" + Helper.GetFactionName(pair.Key, __instance.DataManager) + "</color>" + " joined the war.");
                                        joinedWar = true;
                                        break;
                                    }
                                }
                                if (!joinedWar) {
                                    string Name = Helper.GetFactionShortName(pair.Key, __instance.DataManager) + " VS " + Helper.GetFactionShortName(enemy, __instance.DataManager);
                                    Dictionary<Faction, WarProgression> attackers = new Dictionary<Faction, WarProgression>();
                                    attackers.Add(pair.Key, new WarProgression(new Dictionary<string, Faction>()));
                                    Dictionary<Faction, WarProgression> defenders = new Dictionary<Faction, WarProgression>();
                                    defenders.Add(enemy, new WarProgression(new Dictionary<string, Faction>()));
                                    War war = new War(Name, attackers, defenders);
                                    war.monthlyEvents.Add("<color=" + Fields.settings.attackercolor + ">" + Helper.GetFactionName(pair.Key, __instance.DataManager) + "</color>" + " declared war on " + "<color=" + Fields.settings.defendercolor + ">" + Helper.GetFactionName(enemy, __instance.DataManager) + "</color>" + ".\n");
                                    if (Fields.currentWars.Contains(war)) {
                                        Logger.LogLine(war.name + "already exists");
                                    }
                                    Fields.currentWars.Add(war);
                                }
                            }
                        }
                        else if (Helper.IsAtWar(pair.Key)) {
                            Fields.WarFatique[pair.Key] += Fields.settings.FatiqueLostPerMonth;
                            if (rand.Next(0, 101) < Fields.WarFatique[pair.Key]) {
                                War war = Helper.getWar(pair.Key);
                                if (war == null) {
                                    Logger.LogLine(pair.Key + " has no war at end war calculations");
                                }
                                if (war.duration >= Fields.settings.minMonthDuration) {
                                    if (war.duration < Fields.settings.maxMonthDuration || Fields.settings.maxMonthDuration == -1) {
                                        if (!(Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.Count <= 0) && !(Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders.Count <= 0)) {
                                            string color = "";
                                            if (Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.ContainsKey(pair.Key)) {
                                                color = Fields.settings.attackercolor;
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("\n<color=" + color + ">" + Helper.GetFactionName(pair.Key, __instance.DataManager) + "</color>" + " surrendered.");
                                                foreach (KeyValuePair<string, Faction> taken in Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers[pair.Key].takenPlanets) {
                                                    if (war.defenders.ContainsKey(taken.Value)) {
                                                        StarSystem changedSystem = __instance.StarSystems.Find(x => x.Name.Equals(taken.Key));
                                                        PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(changedSystem.Name));
                                                        FactionControl oldControl = planetState.factionList.FirstOrDefault(x => x.faction == pair.Key);
                                                        int percentage = oldControl.percentage;
                                                        oldControl.percentage = 0;
                                                        FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == taken.Value);
                                                        ownerControl.percentage += percentage;
                                                        changedSystem = Helper.ChangeOwner(changedSystem, ownerControl, __instance, true, true);
                                                        changedSystem = Helper.ChangeWarDescription(changedSystem, __instance);
                                                        Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("<color=" + color + ">" + Helper.GetFactionName(pair.Key, __instance.DataManager) + "</color>" + " returned " + "<color=" + Fields.settings.planetcolor + ">" + changedSystem.Name + "</color>" + " to " + "<color=" + Fields.settings.defendercolor + ">" + Helper.GetFactionName(taken.Value, __instance.DataManager) + "</color>");
                                                    }
                                                }
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.Remove(pair.Key);
                                            }
                                            else {
                                                color = Fields.settings.defendercolor;
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("\n<color=" + color + ">" + Helper.GetFactionName(pair.Key, __instance.DataManager) + "</color>" + " surrendered.");
                                                foreach (KeyValuePair<string, Faction> taken in Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders[pair.Key].takenPlanets) {
                                                    if (war.attackers.ContainsKey(taken.Value)) {
                                                        StarSystem changedSystem = __instance.StarSystems.Find(x => x.Name.Equals(taken.Key));
                                                        PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(changedSystem.Name));
                                                        FactionControl oldControl = planetState.factionList.FirstOrDefault(x => x.faction == pair.Key);
                                                        int percentage = oldControl.percentage;
                                                        oldControl.percentage = 0;
                                                        FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == taken.Value);
                                                        ownerControl.percentage += percentage;
                                                        changedSystem = Helper.ChangeOwner(changedSystem, ownerControl, __instance, true, true);
                                                        changedSystem = Helper.ChangeWarDescription(changedSystem, __instance);
                                                        Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("<color=" + color + ">" + Helper.GetFactionName(pair.Key, __instance.DataManager) + "</color>" + " returned " + "<color=" + Fields.settings.planetcolor + ">" + changedSystem.Name + "</color>" + " to " + "<color=" + Fields.settings.attackercolor + ">" + Helper.GetFactionName(taken.Value, __instance.DataManager) + "</color>");
                                                    }
                                                }
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders.Remove(pair.Key);
                                            }

                                            if (Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers.Count <= 0) {
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("\n<b>Attacking side lost the war.</b>");
                                                foreach(KeyValuePair<Faction, WarProgression> defender in Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders) {
                                                    int count = Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders[defender.Key].takenPlanets.Count;
                                                    Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("<color=" + Fields.settings.defendercolor + ">" + Helper.GetFactionName(defender.Key, __instance.DataManager) + "</color>" + " took " + "<color=" + Fields.settings.planetcolor + ">" + count + "</color>"  + " systems in the war.");
                                                }
                                                if (!Fields.removeWars.Contains(war.name)) {
                                                    Fields.removeWars.Add(war.name);
                                                }
                                            }
                                            else if (Fields.currentWars.Find(x => x.name.Equals(war.name)).defenders.Count <= 0) {
                                                Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("\n<b>Defending side lost the war.</b>");
                                                foreach (KeyValuePair<Faction, WarProgression> attackers in Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers) {
                                                    int count = Fields.currentWars.Find(x => x.name.Equals(war.name)).attackers[attackers.Key].takenPlanets.Count;
                                                    Fields.currentWars.Find(x => x.name.Equals(war.name)).monthlyEvents.Add("<color=" + Fields.settings.attackercolor + ">" + Helper.GetFactionName(attackers.Key, __instance.DataManager) + "</color>" + " took " + "<color=" + Fields.settings.planetcolor + ">" + count + "</color>" + "systems in the war.");
                                                }
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
                        if (!Fields.removeWars.Contains(war.name)) {
                            war.monthlyEvents.Add("<color=" + Fields.settings.attackercolor + ">" + "\nAttackers:" + "</color>");
                            foreach (Faction fac in war.attackers.Keys) {
                                war.monthlyEvents.Add(Helper.GetFactionName(fac, __instance.DataManager) + " | Exhaustion: " + Fields.WarFatique[fac] + "%");
                            }
                            war.monthlyEvents.Add("<color=" + Fields.settings.defendercolor + ">" + "\nDefenders:" + "</color>");
                            foreach (Faction fac in war.defenders.Keys) {
                                war.monthlyEvents.Add(Helper.GetFactionName(fac, __instance.DataManager) + " | Exhaustion: " + Fields.WarFatique[fac] + "%");
                            }
                        }
                        for (int i = 0; i < war.monthlyEvents.Count; i++) {
                            war.monthlyEvents[i] = war.monthlyEvents[i];
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
            }
        }
    }
}