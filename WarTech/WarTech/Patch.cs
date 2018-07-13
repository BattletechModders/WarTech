using BattleTech;
using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using BattleTech.UI;
using Harmony;
using HBS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WarTech {

    [HarmonyPatch(typeof(Contract), "CompleteContract")]
    public static class Contract_CompleteContract_Patch {

        static void Postfix(Contract __instance, MissionResult result) {
            try {
                GameInstance game = LazySingletonBehavior<UnityGameInstance>.Instance.Game;
                StarSystem system = game.Simulation.StarSystems.Find(x => x.ID == __instance.TargetSystem);
                Faction oldOwner = system.Def.Owner;
                if(result == MissionResult.Victory) {
                    system = Helper.PlayerAttackSystem(system, game.Simulation, __instance.Override.employerTeam.faction, __instance.Override.targetTeam.faction, true);
                } else {
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
                //DAILY
                Random rand = new Random();
                for (int i = 0; i < Fields.settings.SystemsPerTick; i++) {
                    StarSystem system;
                    do {
                        system = __instance.StarSystems[rand.Next(0, __instance.StarSystems.Count)];
                    } while (Helper.IsExcluded(system.Owner));
                    system = Helper.AttackSystem(system, __instance);
                }

                //MONTHLY
                int num = (timeLapse <= 0) ? 1 : timeLapse;
                if ((__instance.DayRemainingInQuarter - num <= 0)) {
                    Helper.RefreshResources(__instance);
                    Helper.RefreshEnemies(__instance);
                    Dictionary<Faction, FactionDef> factions = (Dictionary<Faction, FactionDef>)AccessTools.Field(typeof(SimGameState), "factions").GetValue(__instance);
                    foreach (KeyValuePair<Faction, FactionDef> pair in factions) {
                        if (Fields.currentEnemies.ContainsKey(pair.Key)) {
                            Faction[] enemies = Fields.currentEnemies[pair.Key].ToArray();
                            ReflectionHelper.InvokePrivateMethode(pair.Value, "set_Enemies", new object[] { enemies });
                        }
                    }

                    List<string> changeList = new List<string>();
                    foreach (KeyValuePair<string, string> changes in Fields.thisMonthChanges) {
                        StarSystem changedSystem = __instance.StarSystems.Find(x => x.Name.Equals(changes.Key));
                        if (!Helper.GetFactionName(changedSystem.Owner, __instance.DataManager).Equals(changes.Value)) {
                            changeList.Add(Helper.GetFactionName(changedSystem.Owner,__instance.DataManager) + " took " + changes.Key + " from " + changes.Value);
                        }
                    }
                    if (changeList.Count == 0) {
                        changeList.Add("No changes this month");
                    }
                    Fields.thisMonthChanges = new Dictionary<string, string>();
                    List<string> factionResourceList = new List<string>();
                    foreach (FactionResources resources in Fields.factionResources) {
                        factionResourceList.Add(Helper.GetFactionName(resources.faction, __instance.DataManager) + ": \nOffence: "+resources.offence+ "\nDefence: " + resources.defence); 
                    }
                    __instance.StopPlayMode();
                    SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(__instance);
                    interruptQueue.QueuePauseNotification("Monthly Faction Strenght", string.Join("\n ", factionResourceList.ToArray()) + "\n", __instance.GetCrewPortrait(SimGameCrew.Crew_Darius), string.Empty, null, "Understood", null, string.Empty);
                    interruptQueue = (SimGameInterruptManager)AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(__instance);
                    interruptQueue.QueuePauseNotification("Monthly War Report", string.Join("\n ", changeList.ToArray()) + "\n", __instance.GetCrewPortrait(SimGameCrew.Crew_Sumire), string.Empty, null, "Understood", null, string.Empty);

                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }
}