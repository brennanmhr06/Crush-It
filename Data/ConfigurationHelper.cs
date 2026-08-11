using Microsoft.Extensions.Configuration;
using CrushIt.Core;

namespace CrushIt.Data
{
    public static class ConfigurationHelper
    {
        private static IConfiguration? _configuration;
        private static MongoDbSettings? _mongoDbSettings;
        private static SoundSettings? _soundSettings;

        public static void Initialize()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

            _configuration = builder.Build();
            _mongoDbSettings = _configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
            _soundSettings = _configuration.GetSection("SoundSettings").Get<SoundSettings>();
            
            if (_soundSettings != null)
            {
                SoundManager.LoadSettings(_soundSettings);
            }
        }

        public static string GetMongoConnectionString()
        {
            if (_mongoDbSettings == null)
            {
                Initialize();
            }
            return _mongoDbSettings?.ConnectionString ?? string.Empty;
        }

        public static string GetDatabaseName()
        {
            if (_mongoDbSettings == null)
            {
                Initialize();
            }
            return _mongoDbSettings?.DatabaseName ?? string.Empty;
        }

        public static SoundSettings GetSoundSettings()
        {
            if (_soundSettings == null)
            {
                Initialize();
            }
            return _soundSettings ?? new SoundSettings();
        }
    }
}


