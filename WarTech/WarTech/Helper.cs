using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using Harmony;
using HBS.Collections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WarTech {

    public class SaveFields {
        public Dictionary<Faction, List<Faction>> Allies = null;
        public Dictionary<Faction, float> WarFatique = new Dictionary<Faction, float>();
        public List<War> currentWars = new List<War>();
        public bool warmission = false;
        public List<PlanetControlState> stateOfWar = null;
        public Dictionary<string, string> thisMonthChanges = new Dictionary<string, string>();
        public List<FactionResources> factionResources = null;
        public SaveFields(List<PlanetControlState> stateOfWar, Dictionary<string, string> thisMonthChanges, List<FactionResources> factionResources, bool warmission, Dictionary<Faction, float> WarFatique, List<War> currentWars, Dictionary<Faction, List<Faction>> Allies) {
            this.stateOfWar = stateOfWar;
            this.thisMonthChanges = thisMonthChanges;
            this.factionResources = factionResources;
            this.warmission = warmission;
            this.currentWars = currentWars;
            this.WarFatique = WarFatique;
            this.Allies = Allies;
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

        public static StarSystem AttackSystem(StarSystem system, SimGameState Sim, Faction Attacker, System.Random rand) {
            try {



                PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(system.Name));
                FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == system.Owner);
                planetState = CalculateActualAttacks(system, planetState, Attacker, system.Owner, false, rand);

                /*PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(system.Name));
                FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == system.Owner);
                foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                    if (neigbourSystem.Owner != system.Owner && !IsExcluded(neigbourSystem.Owner)) {
                        planetState = CalculateActualAttacks(system, planetState, neigbourSystem.Owner, system.Owner, false);
                    }
                }*/

                return EvaluateOwnerChange(ownerControl, planetState, system, Sim);
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static PlanetControlState CalculateActualAttacks(StarSystem system, PlanetControlState planetState, Faction benefactor, Faction initialTarget, bool player, System.Random rand) {
            try {
                int percentageLeft;
                FactionControl highestControl = planetState.factionList.FirstOrDefault(x => x.faction == initialTarget);
                FactionControl attackerControl = planetState.factionList.FirstOrDefault(x => x.faction == benefactor);
                if (attackerControl == null) {
                    attackerControl = new FactionControl(0, benefactor);
                    planetState.factionList.Add(attackerControl);
                }
                if (player) {
                    percentageLeft = Fields.settings.AttackPercentagePerPlayerMission;
                }
                else {
                    if (Fields.factionResources.Find(x => x.faction == benefactor).offence > 0 && Fields.factionResources.Find(x => x.faction == initialTarget).defence > 0) {
                        int maxAttack = Mathf.Min(100 - attackerControl.percentage, Fields.factionResources.Find(x => x.faction == benefactor).offence);
                        int maxDefence = Mathf.Min(100 - attackerControl.percentage, Fields.factionResources.Find(x => x.faction == initialTarget).defence);
                        int randomAttack = rand.Next(maxAttack) + 1;
                        int randomDefence = rand.Next(maxDefence) + 1;
                        if (randomAttack > randomDefence) {
                            if (Fields.WarFatique[initialTarget] < 100) {
                                Fields.WarFatique[initialTarget] = Mathf.Min(100, Fields.WarFatique[initialTarget] + Fields.settings.FatiquePerLostAttack);
                            }
                        }
                        else {
                            if (Fields.WarFatique[benefactor] < 100) {
                                Fields.WarFatique[benefactor] = Mathf.Min(100, Fields.WarFatique[benefactor] + Fields.settings.FatiquePerLostAttack);
                            }
                        }
                        percentageLeft = Mathf.Max(0, randomAttack - randomDefence);
                        Fields.factionResources.Find(x => x.faction == benefactor).offence -= randomAttack;
                        Fields.factionResources.Find(x => x.faction == initialTarget).defence -= randomDefence;
                        if (Fields.settings.debug) {
                            Logger.LogAttacks(system.Name, benefactor, randomAttack, initialTarget, randomDefence);
                        }
                    }
                    else if (Fields.factionResources.Find(x => x.faction == benefactor).offence > 0) {
                        percentageLeft = 0;
                    }
                    else {
                        if (Fields.WarFatique[initialTarget] < 100) {
                            Fields.WarFatique[initialTarget] = Mathf.Min(100, Fields.WarFatique[initialTarget] + Fields.settings.FatiquePerLostAttack);
                        }
                        int attack = Mathf.Min(100 - attackerControl.percentage, Fields.factionResources.Find(x => x.faction == benefactor).offence);
                        percentageLeft = attack;
                        Fields.factionResources.Find(x => x.faction == benefactor).offence -= attack;
                    }

                }

                do {
                    highestControl = planetState.factionList.FirstOrDefault(x => x.faction == initialTarget);
                    if (highestControl.percentage == 0) {
                        highestControl = planetState.factionList.FindAll(x => x.faction != benefactor).OrderByDescending(i => i.percentage).First();
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
                System.Random rand = new System.Random();
                if (win) {
                    planetState = CalculateActualAttacks(system, planetState, employee, target, true, rand);
                }
                else {
                    planetState = CalculateActualAttacks(system, planetState, target, employee, true, rand);
                }
                return EvaluateOwnerChange(ownerControl, planetState, system, Sim);
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static void Diplomacy(Faction diplomat, ref SimGameState Sim, System.Random rand) {
            try {
                if (rand.Next(0, 2) == 1) {
                    Settle(diplomat, ref Sim);
                }
                NegotiateAllies(diplomat, Sim, rand);
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static void NegotiateAllies(Faction diplomat, SimGameState Sim, System.Random rand) {
            try {
                foreach (KeyValuePair<Faction, FactionDef> faction in Sim.FactionsDict) {
                    if (IsExcluded(faction.Key) || diplomat == faction.Key || IsAtWar(faction.Key)) {
                        continue;
                    }
                    if (!Fields.Allies[diplomat].Contains(faction.Key)) {
                        int divider = Fields.Allies[diplomat].Count + 1;
                        if (rand.Next(1, 101) <= Fields.settings.BaseAllyChance / divider) {
                            Fields.Allies[diplomat].Add(faction.Key);
                            Fields.Allies[faction.Key].Add(diplomat);
                            Fields.DiplomacyLog.Add("<color=" + Fields.settings.neutralcolor + ">" + GetFactionName(diplomat, Sim.DataManager) + "</color>" + " <color=" + Fields.settings.defendercolor + ">" + " signed</color>" + " a non-aggression pact with " + "<color=" + Fields.settings.neutralcolor + ">" + GetFactionName(faction.Key, Sim.DataManager) + "</color>");
                        }
                    }
                    else if (Fields.Allies[diplomat].Contains(faction.Key)) {
                        int divider = Fields.Allies[diplomat].Count + 1;
                        if (rand.Next(1, 101) >= Fields.settings.BaseAllyChance / divider) {
                            Fields.Allies[diplomat].Remove(faction.Key);
                            Fields.Allies[faction.Key].Remove(diplomat);
                            Fields.DiplomacyLog.Add("<color=" + Fields.settings.neutralcolor + ">" + GetFactionName(diplomat, Sim.DataManager) + "</color>" + " <color=" + Fields.settings.attackercolor + ">" + " annulled</color>" + " the non-aggression pact with " + "<color=" + Fields.settings.neutralcolor + ">" + GetFactionName(faction.Key, Sim.DataManager) + "</color>");
                        }
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static void Settle(Faction diplomat, ref SimGameState Sim) {
            try {
                List<StarSystem> diplomatSystems = new List<StarSystem>();
                foreach (StarSystem system in Sim.StarSystems) {
                    if (system.Owner == diplomat) {
                        diplomatSystems.Add(system);
                    }
                }
                diplomatSystems.Shuffle();
                bool foundSettlement = false;
                foreach (StarSystem system in diplomatSystems) {
                    foreach (StarSystem system2 in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                        Faction oldOwner = system2.Owner;
                        if (oldOwner == Faction.Locals || oldOwner == Faction.NoFaction) {
                            StarSystem changedSystem = Sim.StarSystems.Find(x => x.Name.Equals(system2.Name));
                            PlanetControlState planetState = Fields.stateOfWar.FirstOrDefault(x => x.system.Equals(changedSystem.Name));
                            FactionControl oldControl = planetState.factionList.FirstOrDefault(x => x.faction == oldOwner);
                            int percentage = oldControl.percentage;
                            oldControl.percentage = 0;
                            FactionControl ownerControl = planetState.factionList.FirstOrDefault(x => x.faction == diplomat);
                            if (ownerControl == null) {
                                ownerControl = new FactionControl(0, diplomat);
                                planetState.factionList.Add(ownerControl);
                            }
                            ownerControl.percentage += percentage;
                            changedSystem = Helper.ChangeOwner(changedSystem, ownerControl, Sim, true, false);
                            changedSystem = Helper.ChangeWarDescription(changedSystem, Sim);
                            if (oldOwner == Faction.Locals) {
                                Fields.DiplomacyLog.Add("<color=" + Fields.settings.defendercolor + ">" + GetFactionName(diplomat, Sim.DataManager) + "</color>" + " integrated " + "<color=" + Fields.settings.planetcolor + ">" + changedSystem.Name + "</color>" + " into their realm.");
                            }
                            else if (oldOwner == Faction.NoFaction) {
                                Fields.DiplomacyLog.Add("<color=" + Fields.settings.defendercolor + ">" + GetFactionName(diplomat, Sim.DataManager) + "</color>" + " colonized " + "<color=" + Fields.settings.planetcolor + ">" + changedSystem.Name + "</color>" + ".");
                            }
                            foundSettlement = true;
                            break;
                        }
                    }
                    if (foundSettlement) {
                        break;
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
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
                        system = Helper.ChangeOwner(system, newowner, Sim, true, false);
                        planetState.owner = newowner.faction;
                        Fields.WarFatique[newowner.faction] = Mathf.Max(0, Fields.WarFatique[newowner.faction] - Fields.settings.FatiquePerPlanetCapture);
                        Fields.WarFatique[ownerControl.faction] = Mathf.Min(100, Fields.WarFatique[ownerControl.faction] + Fields.settings.FatiquePerPlanetCapture);
                    }
                    else {
                        FactionControl localcontrol = planetState.factionList.FirstOrDefault(x => x.faction == Faction.Locals);
                        if (localcontrol == null) {
                            localcontrol = new FactionControl(0, Faction.Locals);
                            planetState.factionList.Add(localcontrol);
                        }
                        system = Helper.ChangeOwner(system, localcontrol, Sim, true, false);
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
            try {
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
                if (Sim.Starmap != null) {
                    foreach (StarSystem system in Sim.StarSystems) {
                        FactionResources resources = Fields.factionResources.Find(x => x.faction == system.Owner);
                        if (resources != null) {
                            if (!IsExcluded(resources.faction)) {
                                resources.offence += Mathf.RoundToInt(GetOffenceValue(system) * (1 - (Fields.WarFatique[system.Owner] / 100)));
                                resources.defence += Mathf.RoundToInt(GetDefenceValue(system) * (1 - (Fields.WarFatique[system.Owner] / 100)));
                            }
                        }
                    }
                }
                if (Fields.settings.debug) {
                    foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict) {
                        if (!IsExcluded(pair.Key)) {
                            FactionResources resources = Fields.factionResources.Find(x => x.faction == pair.Key);
                            Logger.LogMonthlyReport(Helper.GetFactionName(pair.Key, Sim.DataManager) + " Exhaustion:" + Fields.WarFatique[pair.Key] + " Attack:" + resources.offence + " Defence:" + resources.defence);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static int GetNeighborValue(Dictionary<Faction, int> factionNeighbours, Faction key) {
            try {
                if (factionNeighbours.ContainsKey(key)) {
                    return factionNeighbours[key];
                }
                else {
                    return 0;
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return 0;
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
                foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict) {
                    if (!IsExcluded(pair.Key)) {
                        List<Faction> enemies = new List<Faction>();
                        enemies.Add(Faction.Locals);
                        enemies.Add(Faction.AuriganPirates);
                        foreach (War war in Fields.currentWars) {
                            if (war.attackers.ContainsKey(pair.Key)) {
                                foreach (Faction faction in war.defenders.Keys) {
                                    if (!enemies.Contains(faction)) {
                                        enemies.Add(faction);
                                    }
                                }
                            }
                            else if (war.defenders.ContainsKey(pair.Key)) {
                                foreach (Faction faction in war.attackers.Keys) {
                                    if (!enemies.Contains(faction)) {
                                        enemies.Add(faction);
                                    }
                                }
                            }
                        }
                        Fields.currentEnemies.Add(pair.Key, enemies);
                    }
                    else {
                        if (!Fields.currentEnemies.ContainsKey(pair.Key)) {
                            List<Faction> enemies = new List<Faction>();
                            foreach (KeyValuePair<Faction, FactionDef> pair2 in Sim.FactionsDict) {
                                if (pair2.Key != pair.Key) {
                                    enemies.Add(pair2.Key);
                                }
                            }
                            Fields.currentEnemies.Add(pair.Key, enemies);
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

        public static StarSystem ChangeOwner(StarSystem system, FactionControl control, SimGameState Sim, bool battle, bool lostWar) {
            try {
                if (battle) {
                    Faction oldOwner = system.Owner;
                    Faction newOwner = control.faction;
                    if (!lostWar) {
                        War war = getWar(newOwner);
                        if (war != null) {
                            if (war.attackers.ContainsKey(newOwner)) {
                                if (war.attackers[newOwner].takenPlanets.ContainsKey(system.Name)) {
                                    war.attackers[newOwner].takenPlanets[system.Name] = system.Owner;
                                }
                                else {
                                    war.attackers[newOwner].takenPlanets.Add(system.Name, system.Owner);
                                }
                            }
                            else {
                                if (war.defenders[newOwner].takenPlanets.ContainsKey(system.Name)) {
                                    war.defenders[newOwner].takenPlanets[system.Name] = system.Owner;
                                }
                                else {
                                    war.defenders[newOwner].takenPlanets.Add(system.Name, system.Owner);
                                }
                            }
                        }
                    }
                    TargetSystem target = null;
                    if (control.faction != Faction.Locals && Fields.availableTargets.Count > 0) {
                        target = Fields.availableTargets[newOwner].Find(x => x.system == system);
                        Fields.availableTargets[newOwner].Remove(target);
                    }
                    string factiontag = GetFactionTag(oldOwner);
                    if (!string.IsNullOrEmpty(factiontag))
                        system.Tags.Remove(factiontag);
                    factiontag = GetFactionTag(newOwner);
                    if (!string.IsNullOrEmpty(factiontag))
                        system.Tags.Add(factiontag);
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Owner", new object[] { control.faction });
                    if (target != null) {
                        target = new TargetSystem(system, new Dictionary<Faction, int>());
                        target.CalculateNeighbours(Sim);
                        if (Fields.availableTargets[oldOwner].Find(x => x.system.Name == system.Name) != null) {
                            int index = Fields.availableTargets[oldOwner].FindIndex(x => x.system.Name == system.Name);
                            Fields.availableTargets[oldOwner][index] = target;
                        }
                        else {
                            Fields.availableTargets[oldOwner].Add(target);
                        }
                    }
                }
                if (IsBorder(system, Sim) && Sim.Starmap != null) {
                    system.Tags.Add("planet_other_battlefield");
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Difficulty", new object[] { Fields.settings.BorderPlanetDifficulty });
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_UseMaxContractOverride", new object[] { true });
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_MaxContractOverride", new object[] { Sim.Constants.Story.MaxContractsPerSystem + Sim.Constants.Story.MaxContractsPerSystem / 2 });
                }
                else {
                    system.Tags.Remove("planet_other_battlefield");
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_Difficulty", new object[] { Fields.settings.InlandPlanetDifficulty });
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_UseMaxContractOverride", new object[] { false });
                }
                ReflectionHelper.InvokePrivateMethode(system.Def, "set_ContractEmployers", new object[] { GetEmployees(system, Sim) });
                ReflectionHelper.InvokePrivateMethode(system.Def, "set_ContractTargets", new object[] { GetTargets(system, Sim) });

                if (system.Owner == Faction.NoFaction) {
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_UseMaxContractOverride", new object[] { true });
                    ReflectionHelper.InvokePrivateMethode(system.Def, "set_MaxContractOverride", new object[] { 0 });
                    ReflectionHelper.InvokePrivateMethode(system, "set_CurMaxContracts", new object[] { 0 });
                }
                return system;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static StarSystem ChangeWarDescription(StarSystem system, SimGameState Sim) {
            try {
                if (IsBorder(system, Sim)) {
                    List<string> factionList = new List<string>();
                    factionList.Add("Current Control:");
                    foreach (FactionControl fc in Fields.stateOfWar.Find(x => x.system.Equals(system.Name)).factionList.OrderByDescending(x => x.percentage)) {
                        if (fc.percentage != 0) {
                            factionList.Add(Helper.GetFactionName(fc.faction, Sim.DataManager) + ": " + fc.percentage + "%");
                        }
                    }
                    if (!Fields.FluffDescriptions.ContainsKey(system.Name)) {
                        Fields.FluffDescriptions.Add(system.Name, system.Def.Description.Details);
                    }
                    ReflectionHelper.InvokePrivateMethode(system.Def.Description, "set_Details", new object[] { string.Join("\n", factionList.ToArray()) });
                }
                else if (Fields.FluffDescriptions.ContainsKey(system.Name)) {
                    ReflectionHelper.InvokePrivateMethode(system.Def.Description, "set_Details", new object[] { Fields.FluffDescriptions[system.Name] });
                    Fields.FluffDescriptions.Remove(system.Name);
                }
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
                if (Sim.Starmap != null && !IsExcluded(system.Owner) && Fields.currentEnemies != null) {
                    foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                        if (Fields.currentEnemies.ContainsKey(system.Owner)) {
                            if (system.Owner != neigbourSystem.Owner && !IsExcluded(neigbourSystem.Owner) && Fields.currentEnemies[system.Owner].Contains(neigbourSystem.Owner)) {
                                result = true;
                                break;
                            }
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

        public static War getWar(Faction faction) {
            try {
                foreach (War war in Fields.currentWars) {
                    if (war.attackers.ContainsKey(faction) || war.defenders.ContainsKey(faction)) {
                        return war;
                    }
                }
                return null;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static void RefreshTargets(SimGameState Sim) {
            try {
                Fields.availableTargets = new Dictionary<Faction, List<TargetSystem>>();
                foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict) {
                    if (!IsExcluded(pair.Key)) {
                        Fields.availableTargets.Add(pair.Key, new List<TargetSystem>());
                    }
                }
                if (Sim.Starmap != null) {
                    Fields.neighbourFactions = new Dictionary<Faction, List<Faction>>();
                    foreach (StarSystem system in Sim.StarSystems) {
                        foreach (StarSystem neighbour in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                            if (system.Owner != neighbour.Owner) {
                                if (!Fields.neighbourFactions.ContainsKey(system.Owner)) {
                                    Fields.neighbourFactions.Add(system.Owner, new List<Faction>());
                                }
                                if (!Fields.neighbourFactions[system.Owner].Contains(neighbour.Owner)) {
                                    Fields.neighbourFactions[system.Owner].Add(neighbour.Owner);
                                }
                                if (Fields.currentEnemies[system.Owner].Contains(neighbour.Owner)) {
                                    if (!IsExcluded(neighbour.Owner) && !IsExcluded(system.Owner)) {
                                        TargetSystem target;
                                        if (!Fields.availableTargets.ContainsKey(system.Owner)) {
                                            Fields.availableTargets.Add(system.Owner, new List<TargetSystem>());
                                        }
                                        target = Fields.availableTargets[system.Owner].Find(x => x.system == neighbour);
                                        if (target == null) {
                                            target = new TargetSystem(neighbour, new Dictionary<Faction, int>());
                                            target.CalculateNeighbours(Sim);
                                            Fields.availableTargets[system.Owner].Add(target);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static List<Faction> GetEmployees(StarSystem system, SimGameState Sim) {
            try {
                List<Faction> employees = new List<Faction>();
                if (system.Owner == Faction.NoFaction) {
                    return employees;
                }
                if (!IsExcluded(system.Owner) && Sim.Starmap != null) {
                    employees.Add(Faction.Locals);
                    employees.Add(system.Owner);
                    foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                        if (system.Owner != neigbourSystem.Owner && !employees.Contains(neigbourSystem.Owner) && !IsExcluded(neigbourSystem.Owner)) {
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
                if (system.Owner == Faction.NoFaction) {
                    return targets;
                }
                if (!IsExcluded(system.Owner) && Sim.Starmap != null) {
                    if (system.Owner != Faction.NoFaction) {
                        targets.Add(Faction.Locals);
                        targets.Add(Faction.AuriganPirates);
                        targets.Add(system.Owner);
                        foreach (StarSystem neigbourSystem in Sim.Starmap.GetAvailableNeighborSystem(system)) {
                            if (system.Owner != neigbourSystem.Owner && !targets.Contains(neigbourSystem.Owner) && !IsExcluded(neigbourSystem.Owner)) {
                                targets.Add(neigbourSystem.Owner);
                            }
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

        public static List<Faction> GetFactionsByString(List<string> names) {
            try {
                List<Faction> factions = new List<Faction>();
                foreach (string name in names) {
                    switch (name) {
                        case "AuriganRestoration":
                            factions.Add(Faction.AuriganRestoration);
                            break;
                        case "Betrayers":
                            factions.Add(Faction.Betrayers);
                            break;
                        case "AuriganDirectorate":
                            factions.Add(Faction.AuriganDirectorate); break;
                        case "AuriganMercenaries":
                            factions.Add(Faction.AuriganMercenaries); break;
                        case "AuriganPirates":
                            factions.Add(Faction.AuriganPirates); break;
                        case "ComStar":
                            factions.Add(Faction.ComStar); break;
                        case "Davion":
                            factions.Add(Faction.Davion); break;
                        case "Kurita":
                            factions.Add(Faction.Kurita); break;
                        case "Liao":
                            factions.Add(Faction.Liao); break;
                        case "Locals":
                            factions.Add(Faction.Locals); break;
                        case "MagistracyCentrella":
                            factions.Add(Faction.MagistracyCentrella); break;
                        case "MagistracyOfCanopus":
                            factions.Add(Faction.MagistracyOfCanopus); break;
                        case "MajestyMetals":
                            factions.Add(Faction.MajestyMetals); break;
                        case "Marik":
                            factions.Add(Faction.Marik); break;
                        case "MercenaryReviewBoard":
                            factions.Add(Faction.MercenaryReviewBoard); break;
                        case "Nautilus":
                            factions.Add(Faction.Nautilus); break;
                        case "Steiner":
                            factions.Add(Faction.Steiner); break;
                        case "TaurianConcordat":
                            factions.Add(Faction.TaurianConcordat); break;
                        case "NoFaction":
                            factions.Add(Faction.NoFaction); break;
                        default:
                            break;
                    }
                }
                return factions;
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static string GetFactionTag(Faction faction) {
            try {
                return "planet_faction_" + faction.ToString().ToLower();
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static string GetFactionName(Faction faction, DataManager manager) {
            try {
                return FactionDef.GetFactionDefByEnum(manager, faction).Name.Replace("the ", "").Replace("The ", "");
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static string GetFactionShortName(Faction faction, DataManager manager) {
            try {
                return FactionDef.GetFactionDefByEnum(manager, faction).ShortName.Replace("the ", "").Replace("The ", "");
            }
            catch (Exception ex) {
                Logger.LogError(ex);
                return null;
            }
        }

        public static bool IsAtWar(Faction faction) {
            try {
                bool result = false;
                foreach (War war in Fields.currentWars) {
                    if (war.attackers.ContainsKey(faction) || war.defenders.ContainsKey(faction)) {
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

        public static void SaveState(string instanceGUID, DateTime saveTime) {
            try {
                int unixTimestamp = (int)(saveTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string baseDirectory = Directory.GetParent(Directory.GetParent($"{ WarTech.ModDirectory}").FullName).FullName;
                string filePath = baseDirectory + $"/ModSaves/WarTech/" + instanceGUID + "-" + unixTimestamp + ".json";
                (new FileInfo(filePath)).Directory.Create();
                using (StreamWriter writer = new StreamWriter(filePath, true)) {
                    SaveFields fields = new SaveFields(Fields.stateOfWar, Fields.thisMonthChanges, Fields.factionResources, Fields.warmission, Fields.WarFatique, Fields.currentWars, Fields.Allies);
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
                        Fields.warmission = save.warmission;
                        Fields.WarFatique = save.WarFatique;
                        Fields.currentWars = save.currentWars;
                        Fields.Allies = save.Allies;
                    }
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        public static Contract GetNewWarContract(SimGameState Sim, int Difficulty, Faction emp, Faction targ, StarSystem system) {
            if (Difficulty <= 1) {
                Difficulty = 2;
            }
            else if (Difficulty > 9) {
                Difficulty = 9;
            }
            ContractDifficulty minDiffClamped = (ContractDifficulty)ReflectionHelper.InvokePrivateMethode(Sim, "GetDifficultyEnumFromValue", new object[] { Difficulty });
            ContractDifficulty maxDiffClamped = (ContractDifficulty)ReflectionHelper.InvokePrivateMethode(Sim, "GetDifficultyEnumFromValue", new object[] { Difficulty });
            List<Contract> contractList = new List<Contract>();
            int maxContracts = 1;
            int debugCount = 0;
            while (contractList.Count < maxContracts && debugCount < 1000) {
                WeightedList<MapAndEncounters> contractMaps = new WeightedList<MapAndEncounters>(WeightedListType.SimpleRandom, null, null, 0);
                List<ContractType> contractTypes = new List<ContractType>();
                Dictionary<ContractType, List<ContractOverride>> potentialOverrides = new Dictionary<ContractType, List<ContractOverride>>();
                ContractType[] singlePlayerTypes = (ContractType[])ReflectionHelper.GetPrivateStaticField(typeof(SimGameState), "singlePlayerTypes");
                using (MetadataDatabase metadataDatabase = new MetadataDatabase()) {
                    foreach (Contract_MDD contract_MDD in metadataDatabase.GetContractsByDifficultyRange(Difficulty - 1, Difficulty + 1)) {
                        ContractType contractType = contract_MDD.ContractTypeEntry.ContractType;
                        if (singlePlayerTypes.Contains(contractType)) {
                            if (!contractTypes.Contains(contractType)) {
                                contractTypes.Add(contractType);
                            }
                            if (!potentialOverrides.ContainsKey(contractType)) {
                                potentialOverrides.Add(contractType, new List<ContractOverride>());
                            }
                            ContractOverride item = Sim.DataManager.ContractOverrides.Get(contract_MDD.ContractID);
                            potentialOverrides[contractType].Add(item);
                        }
                    }
                    foreach (MapAndEncounters element in metadataDatabase.GetReleasedMapsAndEncountersByContractTypeAndTags(singlePlayerTypes, system.Def.MapRequiredTags, system.Def.MapExcludedTags, system.Def.SupportedBiomes)) {
                        if (!contractMaps.Contains(element)) {
                            contractMaps.Add(element, 0);
                        }
                    }
                }
                if (contractMaps.Count == 0) {
                    Logger.LogLine("Maps0 break");
                    break;
                }
                if (potentialOverrides.Count == 0) {
                    Logger.LogLine("Overrides0 break");
                    break;
                }
                contractMaps.Reset(false);
                WeightedList<Faction> validEmployers = new WeightedList<Faction>(WeightedListType.SimpleRandom, null, null, 0);
                Dictionary<Faction, WeightedList<Faction>> validTargets = new Dictionary<Faction, WeightedList<Faction>>();

                int i = debugCount;
                debugCount = i + 1;
                WeightedList<MapAndEncounters> activeMaps = new WeightedList<MapAndEncounters>(WeightedListType.SimpleRandom, contractMaps.ToList(), null, 0);
                List<MapAndEncounters> discardedMaps = new List<MapAndEncounters>();

                List<string> mapDiscardPile = (List<string>)ReflectionHelper.GetPrivateField(Sim, "mapDiscardPile");

                for (int j = activeMaps.Count - 1; j >= 0; j--) {
                    if (mapDiscardPile.Contains(activeMaps[j].Map.MapID)) {
                        discardedMaps.Add(activeMaps[j]);
                        activeMaps.RemoveAt(j);
                    }
                }
                if (activeMaps.Count == 0) {
                    mapDiscardPile.Clear();
                    foreach (MapAndEncounters element2 in discardedMaps) {
                        activeMaps.Add(element2, 0);
                    }
                }
                activeMaps.Reset(false);
                MapAndEncounters level = null;
                List<EncounterLayer_MDD> validEncounters = new List<EncounterLayer_MDD>();


                Dictionary<ContractType, WeightedList<PotentialContract>> validContracts = new Dictionary<ContractType, WeightedList<PotentialContract>>();
                WeightedList<PotentialContract> flatValidContracts = null;
                do {
                    level = activeMaps.GetNext(false);
                    if (level == null) {
                        break;
                    }
                    validEncounters.Clear();
                    validContracts.Clear();
                    flatValidContracts = new WeightedList<PotentialContract>(WeightedListType.WeightedRandom, null, null, 0);
                    foreach (EncounterLayer_MDD encounterLayer_MDD in level.Encounters) {
                        ContractType contractType2 = encounterLayer_MDD.ContractTypeEntry.ContractType;
                        if (contractTypes.Contains(contractType2)) {
                            if (validContracts.ContainsKey(contractType2)) {
                                validEncounters.Add(encounterLayer_MDD);
                            }
                            else {
                                foreach (ContractOverride contractOverride2 in potentialOverrides[contractType2]) {
                                    bool flag = true;
                                    ContractDifficulty difficultyEnumFromValue = (ContractDifficulty)ReflectionHelper.InvokePrivateMethode(Sim, "GetDifficultyEnumFromValue", new object[] { contractOverride2.difficulty });
                                    Faction employer2 = Faction.INVALID_UNSET;
                                    Faction target2 = Faction.INVALID_UNSET;
                                    if (difficultyEnumFromValue >= minDiffClamped && difficultyEnumFromValue <= maxDiffClamped) {
                                        employer2 = emp;
                                        target2 = targ;
                                        int difficulty = Sim.NetworkRandom.Int(Difficulty, Difficulty + 1);
                                        system.SetCurrentContractFactions(employer2, target2);
                                        int k = 0;
                                        while (k < contractOverride2.requirementList.Count) {
                                            RequirementDef requirementDef = new RequirementDef(contractOverride2.requirementList[k]);
                                            EventScope scope = requirementDef.Scope;
                                            TagSet curTags;
                                            StatCollection stats;
                                            switch (scope) {
                                                case EventScope.Company:
                                                    curTags = Sim.CompanyTags;
                                                    stats = Sim.CompanyStats;
                                                    break;
                                                case EventScope.MechWarrior:
                                                case EventScope.Mech:
                                                    goto IL_88B;
                                                case EventScope.Commander:
                                                    goto IL_8E9;
                                                case EventScope.StarSystem:
                                                    curTags = system.Tags;
                                                    stats = system.Stats;
                                                    break;
                                                default:
                                                    goto IL_88B;
                                            }
                                            IL_803:
                                            for (int l = requirementDef.RequirementComparisons.Count - 1; l >= 0; l--) {
                                                ComparisonDef item2 = requirementDef.RequirementComparisons[l];
                                                if (item2.obj.StartsWith("Target") || item2.obj.StartsWith("Employer")) {
                                                    requirementDef.RequirementComparisons.Remove(item2);
                                                }
                                            }
                                            if (!SimGameState.MeetsRequirements(requirementDef, curTags, stats, null)) {
                                                flag = false;
                                                break;
                                            }
                                            k++;
                                            continue;
                                            IL_88B:
                                            if (scope != EventScope.Map) {
                                                throw new Exception("Contracts cannot use the scope of: " + requirementDef.Scope);
                                            }
                                            using (MetadataDatabase metadataDatabase2 = new MetadataDatabase()) {
                                                curTags = metadataDatabase2.GetTagSetForTagSetEntry(level.Map.TagSetID);
                                                stats = new StatCollection();
                                                goto IL_803;
                                            }
                                            IL_8E9:
                                            curTags = Sim.CommanderTags;
                                            stats = Sim.CommanderStats;
                                            goto IL_803;
                                        }
                                        if (flag) {
                                            PotentialContract element3 = default(PotentialContract);
                                            element3.contractOverride = contractOverride2;
                                            element3.difficulty = difficulty;
                                            element3.employer = employer2;
                                            element3.target = target2;
                                            validEncounters.Add(encounterLayer_MDD);
                                            if (!validContracts.ContainsKey(contractType2)) {
                                                validContracts.Add(contractType2, new WeightedList<PotentialContract>(WeightedListType.WeightedRandom, null, null, 0));
                                            }
                                            validContracts[contractType2].Add(element3, contractOverride2.weight);
                                            flatValidContracts.Add(element3, contractOverride2.weight);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                while (validContracts.Count == 0 && level != null);
                system.SetCurrentContractFactions(Faction.INVALID_UNSET, Faction.INVALID_UNSET);
                if (validContracts.Count == 0) {
                    if (mapDiscardPile.Count > 0) {
                        mapDiscardPile.Clear();
                    }
                    else {
                        debugCount = 1000;
                        Logger.LogLine(string.Format("[CONTRACT] Unable to find any valid contracts for available map pool. Alert designers.", new object[0]));
                    }
                }
                else {
                    GameContext gameContext = new GameContext(Sim.Context);
                    gameContext.SetObject(GameContextObjectTagEnum.TargetStarSystem, system);
                    Dictionary<ContractType, List<EncounterLayer_MDD>> finalEncounters = new Dictionary<ContractType, List<EncounterLayer_MDD>>();
                    foreach (EncounterLayer_MDD encounterLayer_MDD2 in validEncounters) {
                        ContractType contractType3 = encounterLayer_MDD2.ContractTypeEntry.ContractType;
                        if (!finalEncounters.ContainsKey(contractType3)) {
                            finalEncounters.Add(contractType3, new List<EncounterLayer_MDD>());
                        }
                        finalEncounters[contractType3].Add(encounterLayer_MDD2);
                    }
                    List<PotentialContract> discardedContracts = new List<PotentialContract>();
                    List<string> contractDiscardPile = (List<string>)ReflectionHelper.GetPrivateField(Sim, "contractDiscardPile");
                    for (int m = flatValidContracts.Count - 1; m >= 0; m--) {
                        if (contractDiscardPile.Contains(flatValidContracts[m].contractOverride.ID)) {
                            discardedContracts.Add(flatValidContracts[m]);
                            flatValidContracts.RemoveAt(m);
                        }
                    }
                    if ((float)discardedContracts.Count >= (float)flatValidContracts.Count * Sim.Constants.Story.DiscardPileToActiveRatio || flatValidContracts.Count == 0) {
                        contractDiscardPile.Clear();
                        foreach (PotentialContract element4 in discardedContracts) {
                            flatValidContracts.Add(element4, 0);
                        }
                    }
                    PotentialContract next = flatValidContracts.GetNext(true);
                    ContractType finalContractType = next.contractOverride.contractType;
                    finalEncounters[finalContractType].Shuffle<EncounterLayer_MDD>();
                    string encounterGuid = finalEncounters[finalContractType][0].EncounterLayerGUID;
                    ContractOverride contractOverride3 = next.contractOverride;
                    Faction employer3 = next.employer;
                    Faction target3 = next.target;
                    int targetDifficulty = next.difficulty;
                    Contract con = (Contract)ReflectionHelper.InvokePrivateMethode(Sim, "CreateTravelContract", new object[] { level.Map.MapName, level.Map.MapPath, encounterGuid, finalContractType, contractOverride3, gameContext, employer3, target3, employer3, false, targetDifficulty });
                    mapDiscardPile.Add(level.Map.MapID);
                    contractDiscardPile.Add(contractOverride3.ID);
                    Sim.PrepContract(con, employer3, target3, target3, level.Map.BiomeSkinEntry.BiomeSkin, con.Override.travelSeed, system);
                    contractList.Add(con);
                }
            }
            if (debugCount >= 1000) {
                Logger.LogLine("Unable to fill contract list. Please inform AJ Immediately");
            }
            return contractList[0];
        }
    }
}
