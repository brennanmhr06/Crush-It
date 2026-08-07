using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CrushIt.Data
{
    public class PowerupDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MinLevel { get; set; }
        public int MatchRequirement { get; set; }
        public string SpecialAbility { get; set; } = string.Empty;
        public double ScoreMultiplier { get; set; }
        public double GoldMultiplier { get; set; }
        public string ParticleEffect { get; set; } = string.Empty;
    }

    public class PowerupSettings
    {
        public int ExplosionDuration { get; set; } = 500;
        public int ParticleCount { get; set; } = 20;
        public bool ChainReactionEnabled { get; set; } = true;
        public int MaxChainDepth { get; set; } = 3;
    }

    public class PowerupsConfig
    {
        public List<PowerupDefinition> Powerups { get; set; } = new List<PowerupDefinition>();
        public PowerupSettings PowerupSettings { get; set; } = new PowerupSettings();

        private static PowerupsConfig? _instance;

        public static PowerupsConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadConfig();
                }
                return _instance;
            }
        }

        private static PowerupsConfig LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PowerupsConfig.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<PowerupsConfig>(json) ?? new PowerupsConfig();
                }
            }
            catch
            {
                // Return default config if file not found or invalid
            }
            return new PowerupsConfig();
        }

        public PowerupDefinition? GetPowerup(string name)
        {
            return Powerups.FirstOrDefault(p => p.Name == name);
        }

        public List<PowerupDefinition> GetPowerupsForLevel(int level)
        {
            return Powerups.Where(p => p.MinLevel <= level).ToList();
        }
    }
}
