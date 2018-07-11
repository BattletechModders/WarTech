using BattleTech;
using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using BattleTech.UI;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WarTech {

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
                Logger.LogLine("Populate");
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
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }

    /*[HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_Rehydrate_Patch {
        static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave) {
            try {
                
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }*/

    [HarmonyPatch(typeof(SimGameState), "OnDayPassed")]
    public static class SimGameState_OnDayPassed_Patch {
        static void Prefix(SimGameState __instance, int timeLapse) {
            try {
                if (Fields.stateOfWar == null) {
                    //first init
                    Fields.stateOfWar = new List<PlanetControlState>();
                    foreach (StarSystem system in __instance.StarSystems) {
                        List<FactionControl> factionList = new List<FactionControl>();
                        factionList.Add(new FactionControl(100, system.Owner));
                        Fields.stateOfWar.Add(new PlanetControlState(factionList, system.Name, system.Owner));
                    }
                }

                Random rand = new Random();
                for (int i = 0; i < Fields.settings.SystemsPerTick; i++) {
                    StarSystem system;
                    do {
                        system = __instance.StarSystems[rand.Next(0, __instance.StarSystems.Count)];
                    } while (Helper.IsExcluded(system.Owner));
                    PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(system.Name));
                    FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == system.Owner);
                    foreach (StarSystem neigbourSystem in __instance.Starmap.GetAvailableNeighborSystem(system)) {
                        if (neigbourSystem.Owner != system.Owner) {
                            FactionControl attackerControl = planetState.factionList.FirstOrDefault(x => x.faction == neigbourSystem.Owner);
                            if (attackerControl == null) {
                                attackerControl = new FactionControl(0, neigbourSystem.Owner);
                                planetState.factionList.Add(attackerControl);
                            }
                            ownerControl.percentage -= Fields.settings.AttackPercentagePerTick;
                            attackerControl.percentage += Fields.settings.AttackPercentagePerTick;
                        }
                    }
                    FactionControl newowner = null;
                    FactionControl lastowner = ownerControl;
                    foreach (FactionControl attackerControl in planetState.factionList)
                        if (lastowner.percentage < attackerControl.percentage) {
                            lastowner = attackerControl;
                            newowner = attackerControl;
                        }
                    if (newowner != null) {
                        if (!Fields.thisMonthChanges.ContainsKey(system.Name)) {
                            Fields.thisMonthChanges.Add(system.Name, system.Owner.ToString());
                        }
                        system = Helper.ChangeOwner(system, newowner, __instance, true);
                        planetState.owner = newowner.faction;
                    }
                }
                int num = (timeLapse <= 0) ? 1 : timeLapse;
                if ((__instance.DayRemainingInQuarter - num <= 0)) {
                    Helper.RefreshEnemies(__instance);
                    Dictionary<Faction, FactionDef> factions = (Dictionary<Faction, FactionDef>)AccessTools.Field(typeof(SimGameState), "factions").GetValue(__instance);
                    foreach (KeyValuePair<Faction, FactionDef> pair in factions) {
                        ReflectionHelper.InvokePrivateMethode(pair.Value, "set_Enemies", new object[] { Fields.currentEnemies[pair.Key] });
                    }
                    List<string> changeList = new List<string>();
                    foreach (KeyValuePair<string, string> changes in Fields.thisMonthChanges) {
                        StarSystem changedSystem = __instance.StarSystems.Find(x => x.Name.Equals(changes.Key));
                        changeList.Add(changedSystem.Owner + " took " + changes.Key + " from " + changes.Value);
                    }
                    if (changeList.Count == 0) {
                        changeList.Add("No changes this month");
                    }
                    Fields.thisMonthChanges = new Dictionary<string, string>();
                    __instance.StopPlayMode();
                    SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(__instance);
                    interruptQueue.QueuePauseNotification("Monthly War Report", string.Join("\n ", changeList.ToArray()), __instance.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, null, "Understood", null, string.Empty);
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }
}