using BattleTech;
using System.Collections.Generic;

namespace WarTech {
    public class Settings {
        public int AttackPercentagePerTick = 1;
        public int SystemsPerTick = 100;
        public List<string> excludedFactionNames = new List<string>();
    }
    
    public static class Fields {
       public static List<PlanetControlState> stateOfWar = null;
        public static Dictionary<string, string> thisMonthChanges = new Dictionary<string, string>();
        public static Dictionary<Faction, List<Faction>> currentEnemies = null;
        public static Settings settings;
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
}