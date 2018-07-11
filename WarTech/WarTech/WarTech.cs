using Harmony;
using System.Reflection;

namespace WarTech
{
    public class WarTech
    {
        internal static string ModDirectory;
        public static void Init(string directory, string settingsJSON) {
            ModDirectory = directory;
            Fields.settings = Helper.LoadSettings();
            var harmony = HarmonyInstance.Create("de.morphyum.WarTech");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
