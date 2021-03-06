﻿using BattleTech;
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
        public float priorityContactPayPercentage = 2f;
        public float FatiquePerLostAttack = 0.3f;
        public float FatiqueLostPerMonth = 3f;
        public float FatiqueRecoveredPerMonth = 5f;
        public float FatiquePerPlanetCapture = 1.5f;
        public string attackercolor = "#ee0000ff";
        public string defendercolor = "#00bb00ff";
        public string neutralcolor = "#42f4a7ff";
        public string planetcolor = "#00ffffff";
        public float BaseAllyChance = 50;
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
        public static Dictionary<Faction, List<Faction>> Allies = null;
        public static List<War> currentWars = new List<War>();
        public static List<string> removeWars = new List<string>();
        public static Dictionary<Faction, List<Faction>> neighbourFactions = new Dictionary<Faction, List<Faction>>();
        public static Dictionary<string,string> FluffDescriptions = new Dictionary<string, string>();
        public static List<string> DiplomacyLog = new List<string>();
    }

    public class War {
        public string name;
        public Dictionary<Faction, WarProgression> attackers = new Dictionary<Faction,WarProgression>();
        public Dictionary<Faction, WarProgression> defenders = new Dictionary<Faction, WarProgression>();
        public List<string> monthlyEvents = new List<string>();
        public int duration = 0;
        public War(string name, Dictionary<Faction, WarProgression> attackers, Dictionary<Faction, WarProgression> defenders) {
            this.name = name;
            this.attackers = attackers;
            this.defenders = defenders;
        }
    }

    public class WarProgression {

        //systemName , Original Owner
        public Dictionary<string, Faction> takenPlanets;

        public WarProgression(Dictionary<string, Faction> takenPlanets) {
            this.takenPlanets = takenPlanets;
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