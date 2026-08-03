using Microsoft.Extensions.Configuration;

namespace CrushIt.Data
{
    public static class ConfigurationHelper
    {
        private static IConfiguration? _configuration;
        private static MongoDbSettings? _mongoDbSettings;

        public static void Initialize()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

            _configuration = builder.Build();
            _mongoDbSettings = _configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
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
    }
}


