using BattleTech;
using BattleTech.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WarTech {

    public class SaveFields {
        public List<PlanetControlState> stateOfWar = null;
        public Dictionary<string, string> thisMonthChanges = new Dictionary<string, string>();
        public List<FactionResources> factionResources = null;
        public SaveFields(List<PlanetControlState> stateOfWar, Dictionary<string, string> thisMonthChanges, List<FactionResources> factionResources) {
            this.stateOfWar = stateOfWar;
            this.thisMonthChanges = thisMonthChanges;
            this.factionResources = factionResources;
        }
    }

    public class Helper {
        public static Settings LoadSettings() {
            try {
                using (StreamReader r = new StreamReader($"{ WarTech.ModDirectory}/settings.json")) {
                    string json = r.ReadToEnd();
                    return JsonConvert.DeserializeObject<Settings>(json);
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static StarSystem AttackSystem(StarSystem system, SimGameState Sim) {
            try {

                PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(system.Name));
                FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == system.Owner);
                foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                    if (neigbourSystem.Owner != system.Owner && !IsExcluded(neigbourSystem.Owner)) {
                        planetState = CalculateActualAttacks(system, planetState, neigbourSystem.Owner, system.Owner, false);
                    }
                }
                return EvaluateOwnerChange(ownerControl, planetState, system, Sim);
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static PlanetControlState CalculateActualAttacks(StarSystem system, PlanetControlState planetState, Faction benefactor, Faction initialTarget, bool player) {
            try {

                int percentageLeft;
                if (player) {
                    percentageLeft = Fields.settings.AttackPercentagePerPlayerMission;
                }
                else {
                    percentageLeft = Fields.settings.AttackPercentagePerTick;
                }
                do {
                    FactionControl highestControl = planetState.factionList.FirstOrDefault(x => x.faction == initialTarget);
                    if (highestControl.percentage == 0) {
                        highestControl = planetState.factionList.FindAll(x => x.faction != benefactor).OrderByDescending(i => i.percentage).First();
                    }
                    FactionControl attackerControl = planetState.factionList.FirstOrDefault(x => x.faction == benefactor);
                    if (attackerControl == null) {
                        attackerControl = new FactionControl(0, benefactor);
                        planetState.factionList.Add(attackerControl);
                    }
                    if (attackerControl.percentage == 100) {
                        break;
                    }
                    int realChanges = percentageLeft;
                    if (highestControl.percentage - realChanges < 0) {
                        int leftover = Math.Abs(highestControl.percentage - realChanges);
                        realChanges -= leftover;
                    }
                    if (attackerControl.percentage + realChanges > 100) {
                        int leftover = attackerControl.percentage + realChanges - 100;
                        realChanges -= leftover;
                    }
                    percentageLeft -= realChanges;
                    highestControl.percentage -= realChanges;
                    attackerControl.percentage += realChanges;
                } while (percentageLeft > 0);
                return planetState;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return planetState;
            }
        }

        public static StarSystem PlayerAttackSystem(StarSystem system, SimGameState Sim, Faction employee, Faction target, bool win) {
            try {
                PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(system.Name));
                FactionControl employeeControl = planetState.factionList.FirstOrDefault(x => x.faction == employee);
                FactionControl targetControl = planetState.factionList.FirstOrDefault(x => x.faction == target);
                FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == system.Owner);
                if (employeeControl == null) {
                    employeeControl = new FactionControl(0, employee);
                    planetState.factionList.Add(employeeControl);
                }
                if (targetControl == null) {
                    targetControl = new FactionControl(0, target);
                    planetState.factionList.Add(targetControl);
                }
                if (win) {
                    planetState = CalculateActualAttacks(system, planetState, employee, target, true);
                }
                else {
                    planetState = CalculateActualAttacks(system, planetState, target, employee, true);
                }
                return EvaluateOwnerChange(ownerControl, planetState, system, Sim);
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static StarSystem EvaluateOwnerChange(FactionControl ownerControl, PlanetControlState planetState, StarSystem system, SimGameState Sim) {
            try {
                FactionControl newowner = null;
                FactionControl lastowner = ownerControl;
                foreach (FactionControl attackerControl in planetState.factionList)
                    if (lastowner.percentage < attackerControl.percentage) {
                        lastowner = attackerControl;
                        newowner = attackerControl;
                    }
                if (newowner != null) {
                    if (!Fields.thisMonthChanges.ContainsKey(system.Name)) {
                        Fields.thisMonthChanges.Add(system.Name, Helper.GetFactionName(system.Owner, Sim.DataManager));
                    }
                    if (newowner.percentage >= Fields.settings.PercentageForControl) {
                        system = Helper.ChangeOwner(system, newowner, Sim, true);
                        planetState.owner = newowner.faction;
                    }
                    else {
                        FactionControl localcontrol = planetState.factionList.FirstOrDefault(x => x.faction == Faction.Locals);
                        if (localcontrol == null) {
                            localcontrol = new FactionControl(0, Faction.Locals);
                            planetState.factionList.Add(localcontrol);
                        }
                        system = Helper.ChangeOwner(system, localcontrol, Sim, true);
                        planetState.owner = Faction.Locals;
                    }

                }
                return Helper.ChangeWarDescription(system, Sim);
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static void RefreshResources(SimGameState Sim) {
            foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict) {
                if (!IsExcluded(pair.Key)) {
                    if (Fields.factionResources.Find(x => x.faction == pair.Key) == null) {
                        Fields.factionResources.Add(new FactionResources(pair.Key, 0, 0));
                    }
                    else {
                        FactionResources resources = Fields.factionResources.Find(x => x.faction == pair.Key);
                        resources.offence = 0;
                        resources.defence = 0;
                    }
                }
            }
            foreach (StarSystem system in Sim.StarSystems) {
                FactionResources resources = Fields.factionResources.Find(x => x.faction == system.Owner);
                if (resources != null) {
                    resources.offence += GetOffenceValue(system);
                    resources.defence += GetDefenceValue(system);
                }
            }
        }

        /*
            ATTACK POINTS
            planet_industry_poor
            planet_industry_mining
            planet_industry_rich
            planet_industry_manufacturing
            planet_industry_research
            planet_other_starleague
            */
        public static int GetOffenceValue(StarSystem system) {
            try {
                int result = 0;
                if (system.Tags.Contains("planet_industry_poor"))
                    result += -1;
                if (system.Tags.Contains("planet_industry_mining"))
                    result += 1;
                if (system.Tags.Contains("planet_industry_rich"))
                    result += 2;
                if (system.Tags.Contains("planet_industry_rich"))
                    result += 3;
                if (system.Tags.Contains("planet_industry_manufacturing"))
                    result += 4;
                if (system.Tags.Contains("planet_industry_research"))
                    result += 5;
                if (system.Tags.Contains("planet_other_starleague"))
                    result += 6;
                return result;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return 0;
            }
        }
        /* DEFENSE POINTS
            planet_pop_none
            planet_pop_small
            planet_pop_medium
             planet_industry_aquaculture
             planet_industry_agriculture
             planet_pop_large
             planet_other_hub
             planet_other_megacity
             planet_other_capital
             */

        public static int GetDefenceValue(StarSystem system) {
            try {
                int result = 0;
                if (system.Tags.Contains("planet_pop_none"))
                    result += -1;
                if (system.Tags.Contains("planet_pop_small"))
                    result += 1;
                if (system.Tags.Contains("planet_pop_medium"))
                    result += 2;
                if (system.Tags.Contains("planet_industry_aquaculture"))
                    result += 3;
                if (system.Tags.Contains("planet_industry_agriculture"))
                    result += 3;
                if (system.Tags.Contains("planet_pop_large"))
                    result += 4;
                if (system.Tags.Contains("planet_other_hub"))
                    result += 5;
                if (system.Tags.Contains("planet_other_megacity"))
                    result += 6;
                if (system.Tags.Contains("planet_other_capital"))
                    result += 7;
                return result;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return 0;
            }
        }

        public static void RefreshEnemies(SimGameState Sim) {
            try {
                Fields.currentEnemies = new Dictionary<Faction, List<Faction>>();
                foreach (StarSystem system in Sim.StarSystems) {
                    if (!IsExcluded(system.Owner)) {
                        foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                            if (system.Owner != neigbourSystem.Owner) {
                                List<Faction> enemies;
                                if (Fields.currentEnemies.ContainsKey(system.Owner)) {
                                    enemies = Fields.currentEnemies[system.Owner];
                                }
                                else {
                                    enemies = new List<Faction>();
                                }

                                if (!enemies.Contains(neigbourSystem.Owner)) {
                                    enemies.Add(neigbourSystem.Owner);
                                }

                                if (Fields.currentEnemies.ContainsKey(system.Owner)) {
                                    Fields.currentEnemies[system.Owner] = enemies;
                                }
                                else {
                                    Fields.currentEnemies.Add(system.Owner, enemies);
                                }
                            }
                        }
                    }
                    else {
                        if (!Fields.currentEnemies.ContainsKey(system.Owner)) {
                            List<Faction> enemies = new List<Faction>();
                            foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict) {
                                if (pair.Key != system.Owner) {
                                    enemies.Add(pair.Key);
                                }
                            }
                            Fields.currentEnemies.Add(system.Owner, enemies);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static bool IsExcluded(Faction faction) {
            try {
                bool result = false;
                if (Fields.settings.excludedFactionNames.Contains(faction.ToString())) {
                    result = true;
                }
                return result;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return false;
            }
        }

        public static StarSystem ChangeOwner(StarSystem system, FactionControl control, SimGameState Sim, bool battle) {
            try {
                if (battle) {
                    string factiontag = GetFactionTag(system.Owner);
                    if (!string.IsNullOrEmpty(factiontag))
                        system.Tags.Remove(factiontag);
                    factiontag = GetFactionTag(control.faction);
                    if (!string.IsNullOrEmpty(factiontag))
                        system.Tags.Add(factiontag);
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Owner", new object[] { control.faction });
                }
                if (IsBorder(system, Sim) && Sim.Starmap != null) {
                    system.Tags.Add("planet_other_battlefield");
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Difficulty", new object[] { 2 });
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_UseMaxContractOverride", new object[] { true });
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_MaxContractOverride", new object[] { Sim.Constants.Story.MaxContractsPerSystem + Sim.Constants.Story.MaxContractsPerSystem / 2 });
                }
                else {
                    system.Tags.Remove("planet_other_battlefield");
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Difficulty", new object[] { 0 });
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_UseMaxContractOverride", new object[] { false });
                }
                ReflectionHelper.InvokePrivateMethode(system.Def, "set_ContractEmployers", new object[] { GetEmployees(system, Sim) });
                ReflectionHelper.InvokePrivateMethode(system.Def, "set_ContractTargets", new object[] { GetTargets(system, Sim) });
                return system;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static StarSystem ChangeWarDescription(StarSystem system, SimGameState Sim) {
            try {
                List<string> factionList = new List<string>();
                factionList.Add("Current Control:");
                foreach (FactionControl fc in Fields.stateOfWar.Find(x => x.system.Equals(system.Name)).factionList.OrderByDescending(x => x.percentage)) {
                    if (fc.percentage != 0) {
                        factionList.Add(Helper.GetFactionName(fc.faction, Sim.DataManager) + ": " + fc.percentage + "%");
                    }
                }
                ReflectionHelper.InvokePrivateMethode(system.Def.Description, "set_Details", new object[] { string.Join("\n", factionList.ToArray()) });
                return system;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static bool IsBorder(StarSystem system, SimGameState Sim) {
            try {
                bool result = false;
                if (Sim.Starmap != null && !IsExcluded(system.Owner)) {
                    foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                        if (system.Owner != neigbourSystem.Owner && !IsExcluded(neigbourSystem.Owner)) {
                            result = true;
                            break;
                        }
                    }
                }
                return result;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return false;
            }
        }

        public static List<Faction> GetEmployees(StarSystem system, SimGameState Sim) {
            try {
                List<Faction> employees = new List<Faction>();

                if (!IsExcluded(system.Owner) && Sim.Starmap != null) {
                    employees.Add(Faction.Locals);
                    employees.Add(system.Owner);
                    foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                        if (system.Owner != neigbourSystem.Owner && !employees.Contains(neigbourSystem.Owner)) {
                            employees.Add(neigbourSystem.Owner);
                        }
                    }
                }
                else {
                    foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict) {
                        employees.Add(pair.Key);
                    }
                }
                return employees;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static List<Faction> GetTargets(StarSystem system, SimGameState Sim) {
            try {
                List<Faction> targets = new List<Faction>();
                if (!IsExcluded(system.Owner) && Sim.Starmap != null) {
                    targets.Add(Faction.Locals);
                    targets.Add(Faction.AuriganPirates);
                    targets.Add(system.Owner);
                    foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                        if (system.Owner != neigbourSystem.Owner && !targets.Contains(neigbourSystem.Owner)) {
                            targets.Add(neigbourSystem.Owner);
                        }
                    }
                }
                else {
                    foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict) {
                        targets.Add(pair.Key);
                    }
                }
                return targets;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static string GetFactionTag(Faction faction) {
            try {
                switch (faction) {
                    case Faction.AuriganRestoration:
                        return "planet_faction_restoration";
                    case Faction.Betrayers:
                        return "planet_faction_outworlds";
                    case Faction.AuriganDirectorate:
                        return "planet_faction_marian";
                    case Faction.AuriganMercenaries:
                        return "planet_faction_illyrian";
                    case Faction.AuriganPirates:
                        return "planet_faction_pirate";
                    case Faction.Davion:
                        return "planet_faction_davion";
                    case Faction.Kurita:
                        return "planet_faction_kurita";
                    case Faction.Liao:
                        return "planet_faction_liao";
                    case Faction.MagistracyCentrella:
                        return "planet_faction_oberon";
                    case Faction.MagistracyOfCanopus:
                        return "planet_faction_magistracy";
                    case Faction.MajestyMetals:
                        return "planet_faction_lothian";
                    case Faction.Marik:
                        return "planet_faction_marik";
                    case Faction.Nautilus:
                        return "planet_faction_circinus";
                    case Faction.Steiner:
                        return "planet_faction_steiner";
                    case Faction.TaurianConcordat:
                        return "planet_faction_taurian";
                    case Faction.MercenaryReviewBoard:
                        return "planet_faction_mrb";
                    default:
                        return "";

                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static string GetFactionName(Faction faction, DataManager manager) {
            try {
                return FactionDef.GetFactionDefByEnum(manager, faction).Name;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static void SaveState(string instanceGUID, DateTime saveTime) {
            try {
                int unixTimestamp = (int)(saveTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string baseDirectory = Directory.GetParent(Directory.GetParent($"{ WarTech.ModDirectory}").FullName).FullName;
                string filePath = baseDirectory + $"/ModSaves/WarTech/" + instanceGUID + "-" + unixTimestamp + ".json";
                (new FileInfo(filePath)).Directory.Create();
                using (StreamWriter writer = new StreamWriter(filePath, true)) {
                    SaveFields fields = new SaveFields(Fields.stateOfWar, Fields.thisMonthChanges, Fields.factionResources);
                    string json = JsonConvert.SerializeObject(fields);
                    writer.Write(json);
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static void LoadState(string instanceGUID, DateTime saveTime) {
            try {
                int unixTimestamp = (int)(saveTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string baseDirectory = Directory.GetParent(Directory.GetParent($"{ WarTech.ModDirectory}").FullName).FullName;
                string filePath = baseDirectory + $"/ModSaves/WarTech/" + instanceGUID + "-" + unixTimestamp + ".json";
                if (File.Exists(filePath)) {
                    using (StreamReader r = new StreamReader(filePath)) {
                        string json = r.ReadToEnd();
                        SaveFields save = JsonConvert.DeserializeObject<SaveFields>(json);
                        Fields.stateOfWar = save.stateOfWar;
                        Fields.thisMonthChanges = save.thisMonthChanges;
                        Fields.factionResources = save.factionResources;
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }
    }
}
