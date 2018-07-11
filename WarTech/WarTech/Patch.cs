using BattleTech;
using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using BattleTech.UI;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using static BattleTech.StarSystemDef;

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

    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_Rehydrate_Patch {
        static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave) {
            try {
                if (Fields.stateOfWar != null) {
                    foreach (PlanetControlState control in Fields.stateOfWar) {
                        StarSystem system = __instance.StarSystems.Find(x => x.Name.Equals(control.system));
                        if (system.Owner != control.owner) {
                            FactionControl factionControl = control.factionList.Find(x => x.faction == control.owner);
                            system = Helper.ChangeOwner(system, factionControl);
                        }
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
                Fields.settings = Helper.LoadSettings();

                if (Fields.stateOfWar == null) {
                    Fields.stateOfWar = new List<PlanetControlState>();
                    foreach (StarSystem system in __instance.StarSystems) {
                        List<FactionControl> factionList = new List<FactionControl>();
                        factionList.Add(new FactionControl(100, system.Owner));
                        Fields.stateOfWar.Add(new PlanetControlState(factionList, system.Name, system.Owner));
                    }
                }

                Random rand = new Random();
                for (int i = 0; i < Fields.settings.SystemsPerTick; i++) {

                    StarSystem system = __instance.StarSystems[rand.Next(0, __instance.StarSystems.Count)];
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
                        __instance.StopPlayMode();
                        system = Helper.ChangeOwner(system, newowner);
                        planetState.owner = newowner.faction;
                        PauseNotification.Show("Takeover", newowner.faction + " took " + system.Name + " from " + ownerControl.faction,
                        __instance.GetCrewPortrait(SimGameCrew.Crew_Darius), string.Empty, true, null, "OK", null, "Cancel");
                    }
                }
            }
            catch (Exception e) {
                Logger.LogError(e);
            }
        }
    }
}