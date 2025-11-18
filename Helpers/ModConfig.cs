using Newtonsoft.Json;
using System.IO;
using TaleWorlds.Library;

namespace TroopTrainingExpanded.Helpers
{
    public class ModConfig
    {
        private static string ConfigPath =>
                    Path.Combine(BasePath.Name, "Modules", "TroopTrainingExpanded", "settings.json");

        public int MaxTrainingTroops { get; set; } = 5;

        private static ModConfig _instance;
        public static ModConfig Instance => _instance ??= Load();

        private static ModConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return CreateDefaultFile();

                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonConvert.DeserializeObject<ModConfig>(json);

                return cfg ?? CreateDefaultFile();
            }
            catch
            {
                return CreateDefaultFile();
            }
        }

        private static ModConfig CreateDefaultFile()
        {
            var cfg = new ModConfig();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(cfg, Formatting.Indented));
            }
            catch { }

            return cfg;
        }
    }
}
