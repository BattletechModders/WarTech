using BattleTech;
using BattleTech.Data;
using System;
using System.IO;

namespace WarTech {
    public class Logger {
        static string filePath = $"{ WarTech.ModDirectory}/Log.txt";
        public static void LogError(Exception ex) {
            (new FileInfo(filePath)).Directory.Create();
            using (StreamWriter writer = new StreamWriter(filePath, true)) {
                writer.WriteLine("Message :" + ex.Message + "<br/>" + Environment.NewLine + "StackTrace :" + ex.StackTrace +
                   "" + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                writer.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
            }
        }

        public static void LogLine(String line) {
            (new FileInfo(filePath)).Directory.Create();
            using (StreamWriter writer = new StreamWriter(filePath, true)) {
                writer.WriteLine(line + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                writer.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
            }
        }

        public static void LogAttacks(string system, Faction attacker, int attack, Faction defender, int defence) {
            string attackFilePath = $"{ WarTech.ModDirectory}/Attacks.txt";
            (new FileInfo(attackFilePath)).Directory.Create();
            using (StreamWriter writer = new StreamWriter(attackFilePath, true)) {
                writer.WriteLine(system);
                writer.WriteLine("Attacked by " + attacker + " with "+ attack);
                writer.WriteLine("Defended by " + defender + " with " + defence);
                writer.WriteLine(attacker + ": offence left: " + Fields.factionResources.Find(x => x.faction == attacker).offence + " defence left: " + Fields.factionResources.Find(x => x.faction == attacker).defence);
                writer.WriteLine(defender + ": offence left: " + Fields.factionResources.Find(x => x.faction == defender).offence + " defence left: " + Fields.factionResources.Find(x => x.faction == defender).defence);
                writer.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
            }
        }
    }
}