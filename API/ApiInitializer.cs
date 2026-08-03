namespace CrushIt.API
{
    public static class ApiInitializer
    {
        private static IApiClient? _apiClient;
        private static AntiCheatService? _antiCheatService;
        private static ApiConfiguration? _currentConfig;

        public static void Initialize(ApiConfiguration configuration)
        {
            _currentConfig = configuration;
            _apiClient = new ApiClient(configuration.BaseUrl, configuration.ApiKey);

            if (configuration.EnableLogging)
            {
                Console.WriteLine($"API initialized with base URL: {configuration.BaseUrl}");
                Console.WriteLine($"Validation enabled: {configuration.EnableValidation}");
            }
        }

        public static IApiClient GetApiClient()
        {
            return _apiClient ?? throw new InvalidOperationException("API not initialized. Call Initialize() first.");
        }

        public static AntiCheatService CreateAntiCheatService(string userId, string sessionId)
        {
            if (_apiClient == null)
                throw new InvalidOperationException("API not initialized. Call Initialize() first.");

            _antiCheatService = new AntiCheatService(_apiClient, userId, sessionId);
            return _antiCheatService;
        }

        public static bool IsInitialized => _apiClient != null;

        public static ApiConfiguration? CurrentConfig => _currentConfig;
    }
}