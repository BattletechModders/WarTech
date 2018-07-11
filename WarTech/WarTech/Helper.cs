using BattleTech;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace WarTech {

    public class SaveFields {
        public List<PlanetControlState> stateOfWar = null;
        public Dictionary<string, string> thisMonthChanges = new Dictionary<string, string>();

        public SaveFields(List<PlanetControlState> stateOfWar, Dictionary<string, string> thisMonthChanges) {
            this.stateOfWar = stateOfWar;
            this.thisMonthChanges = thisMonthChanges;
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

        public static void RefreshEnemies(SimGameState Sim) {
            try {
                Fields.currentEnemies = new Dictionary<Faction, List<Faction>>();
                foreach (StarSystem system in Sim.StarSystems) {
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
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static StarSystem ChangeOwner(StarSystem system, FactionControl control, SimGameState Sim, bool battle) {
            try {
                if (battle) {
                    system.Tags.Remove(GetFactionTag(system.Owner));
                    system.Tags.Add(GetFactionTag(control.faction));
                    system.Tags.Add("planet_other_battlefield");
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Owner", new object[] { control.faction });
                }
                if(IsBorder(system, Sim)) {
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Difficulty", new object[] { 2 });
                } else {
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Difficulty", new object[] { 0 });
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

        public static bool IsBorder(StarSystem system, SimGameState Sim) {
            try {
                bool result = false;
                foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                    if (system.Owner != neigbourSystem.Owner) {
                        result = true;
                        break;
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
                employees.Add(Faction.Locals);
                employees.Add(system.Owner);
                foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                    if (system.Owner != neigbourSystem.Owner && !employees.Contains(neigbourSystem.Owner)) {
                        employees.Add(neigbourSystem.Owner);
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
                targets.Add(Faction.Locals);
                targets.Add(Faction.AuriganPirates);
                targets.Add(system.Owner);
                foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                    if (system.Owner != neigbourSystem.Owner && !targets.Contains(neigbourSystem.Owner)) {
                        targets.Add(neigbourSystem.Owner);
                    }
                }
                return targets;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }
        /*public static List<Faction> getAllies(Faction faction) {
            switch (faction) {
                case Faction.Betrayers:
                    return new List<Faction>() { Faction.Betrayers, Faction.Locals, Faction.ComStar };
                case Faction.AuriganDirectorate:
                    return new List<Faction>() { Faction.AuriganDirectorate, Faction.Locals };
                case Faction.AuriganMercenaries:
                    return new List<Faction>() { Faction.AuriganMercenaries, Faction.Locals };
                case Faction.AuriganPirates:
                    return new List<Faction>() { Faction.AuriganPirates, Faction.Locals };
                case Faction.AuriganRestoration:
                    return new List<Faction>() { Faction.AuriganRestoration, Faction.Locals };
                case Faction.ComStar:
                    return new List<Faction>() { Faction.ComStar, Faction.Locals };
                case Faction.Davion:
                    return new List<Faction>() { Faction.Davion, Faction.ComStar, Faction.Locals };
                case Faction.Kurita:
                    return new List<Faction>() { Faction.ComStar, Faction.Locals, Faction.Kurita };
                case Faction.Liao:
                    return new List<Faction>() { Faction.Liao, Faction.ComStar, Faction.Locals };
                case Faction.Locals:
                    return new List<Faction>() { Faction.Locals };
                case Faction.MagistracyCentrella:
                    return new List<Faction>() { Faction.Locals, Faction.MagistracyCentrella, Faction.AuriganPirates };
                case Faction.MagistracyOfCanopus:
                    return new List<Faction>() { Faction.Locals, Faction.ComStar, Faction.MagistracyOfCanopus };
                case Faction.MajestyMetals:
                    return new List<Faction>() { Faction.Locals, Faction.MajestyMetals };
                case Faction.Marik:
                    return new List<Faction>() { Faction.Locals, Faction.Marik, Faction.ComStar };
                case Faction.MercenaryReviewBoard:
                    return new List<Faction>() { Faction.Locals, Faction.ComStar, Faction.MercenaryReviewBoard };
                case Faction.Nautilus:
                    return new List<Faction>() { Faction.Locals, Faction.Nautilus, Faction.ComStar, Faction.AuriganPirates };
                case Faction.NoFaction:
                    return new List<Faction>() { Faction.Locals, Faction.NoFaction };
                case Faction.Steiner:
                    return new List<Faction>() { Faction.Locals, Faction.Steiner, Faction.ComStar };
                case Faction.TaurianConcordat:
                    return new List<Faction>() { Faction.Locals, Faction.TaurianConcordat, Faction.ComStar };
                default:
                    return new List<Faction>() { Faction.NoFaction };
            }


        }

        public static List<Faction> getEnemies(Faction faction) {
            switch (faction) {
                case Faction.Betrayers:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.MercenaryReviewBoard,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.AuriganDirectorate:
                    return new List<Faction>() { Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.AuriganMercenaries:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.AuriganPirates:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita,Faction.Liao,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.AuriganRestoration:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.ComStar:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.Davion:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.Kurita:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.Davion,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.Liao:
                    return new List<Faction>() { Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.Davion, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.Locals:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.MagistracyCentrella:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Liao,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,Faction.TaurianConcordat };

                case Faction.MagistracyOfCanopus:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,
                        Faction.Betrayers, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.MajestyMetals:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.Marik,Faction.Nautilus,
                        Faction.Steiner};

                case Faction.Marik:
                    return new List<Faction>() {Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.Davion,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MajestyMetals,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.MercenaryReviewBoard:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers,  Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.Nautilus:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,
                        Faction.Steiner,Faction.TaurianConcordat };
                case Faction.NoFaction:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers, Faction.ComStar, Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,
                        Faction.Steiner,Faction.TaurianConcordat };

                case Faction.Steiner:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers,  Faction.Kurita,Faction.Liao,
                        Faction.MagistracyOfCanopus,Faction.MajestyMetals, Faction.Marik,Faction.Nautilus,
                        Faction.TaurianConcordat };
                case Faction.TaurianConcordat:
                    return new List<Faction>() { Faction.AuriganDirectorate,Faction.AuriganMercenaries,Faction.AuriganPirates,Faction.AuriganRestoration,
                        Faction.Betrayers,  Faction.Davion,Faction.Kurita,Faction.Liao, Faction.MagistracyCentrella,
                        Faction.MagistracyOfCanopus, Faction.Nautilus,
                        Faction.Steiner };
                default:
                    return new List<Faction>() { Faction.NoFaction };
            }
        }*/

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

        public static void SaveState(string instanceGUID, DateTime saveTime) {
            try {
                int unixTimestamp = (int)(saveTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string baseDirectory = Directory.GetParent(Directory.GetParent($"{ WarTech.ModDirectory}").FullName).FullName;
                string filePath = baseDirectory + $"/ModSaves/WarTech/" + instanceGUID + "-" + unixTimestamp + ".json";
                (new FileInfo(filePath)).Directory.Create();
                using (StreamWriter writer = new StreamWriter(filePath, true)) {
                    SaveFields fields = new SaveFields(Fields.stateOfWar, Fields.thisMonthChanges);
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
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }
    }
}
