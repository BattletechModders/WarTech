using BattleTech;
using BattleTech.Framework;
using System.Collections.Generic;

namespace WarTech {
    public class Settings {
        public int AttackPercentagePerPlayerMission = 10;
        public List<string> excludedFactionNames = new List<string>();
        public int PercentageForControl = 30;
        public int WeightOfNeighbours = 1;
        public int minMonthDuration = 1;
        public int maxMonthDuration = -1;
        public int InlandPlanetDifficulty = 0;
        public int BorderPlanetDifficulty = 2;
        public float priorityContactPayPercentage = 2f;
        public float FatiquePerLostAttack = 0.5f;
        public float FatiqueLostPerMonth = 3f;
        public float FatiqueRecoveredPerDay = 0.5f;
        public bool debug = false;
    }

    public static class Fields {
        public static List<FactionResources> factionResources = new List<FactionResources>();
        public static List<PlanetControlState> stateOfWar = null;
        public static Dictionary<string, string> thisMonthChanges = new Dictionary<string, string>();
        public static Dictionary<Faction, List<Faction>> currentEnemies = null;
        public static Settings settings;
        public static Dictionary<Faction, List<TargetSystem>> availableTargets = new Dictionary<Faction, List<TargetSystem>>();
        public static bool warmission = false;
        public static Dictionary<Faction, float> WarFatique = null;
        public static List<War> currentWars = new List<War>();
        public static List<string> removeWars = new List<string>();
        public static Dictionary<Faction, List<Faction>> neighbourFactions = new Dictionary<Faction, List<Faction>>();
    }

    public class War {
        public string name;
        public List<Faction> attackers = new List<Faction>();
        public List<Faction> defenders = new List<Faction>();
        public List<string> monthlyEvents = new List<string>();
        public int duration = 0;
        public War(string name, List<Faction> attackers, List<Faction> defenders) {
            this.name = name;
            this.attackers = attackers;
            this.defenders = defenders;
        }
    }

    public class TargetSystem {
        public StarSystem system;
        public Dictionary<Faction, int> factionNeighbours;

        public TargetSystem(StarSystem system, Dictionary<Faction, int> factionNeighbours) {
            this.system = system;
            this.factionNeighbours = factionNeighbours;
        }

        public void CalculateNeighbours(SimGameState Sim) {
            this.factionNeighbours = new Dictionary<Faction, int>();
            foreach(StarSystem system in Sim.Starmap.GetAvailableNeighborSystem(this.system)) {
                if (this.factionNeighbours.ContainsKey(system.Owner)) {
                    this.factionNeighbours[system.Owner] += 1;
                } else {
                    this.factionNeighbours.Add(system.Owner, 1);
                }
            }
        }
    }

    public class FactionResources {
        public int offence;
        public int defence;
        public Faction faction;

        public FactionResources(Faction faction, int offence, int defence) {
            this.faction = faction;
            this.offence = offence;
            this.defence = defence;
        }
    }


    public class PlanetControlState {
        public List<FactionControl> factionList;
        public string system;
        public Faction owner;

        public PlanetControlState(List<FactionControl> factionList, string system, Faction owner) {
            this.factionList = factionList;
            this.system = system;
            this.owner = owner;
        }
    }

    public class FactionControl {
        public int percentage;
        public Faction faction;

        public FactionControl(int percentage, Faction faction) {
            this.faction = faction;
            this.percentage = percentage;
        }
    }

    public struct PotentialContract {
        // Token: 0x040089A4 RID: 35236
        public ContractOverride contractOverride;

        // Token: 0x040089A5 RID: 35237
        public Faction employer;

        // Token: 0x040089A6 RID: 35238
        public Faction target;

        // Token: 0x040089A7 RID: 35239
        public int difficulty;
    }
}