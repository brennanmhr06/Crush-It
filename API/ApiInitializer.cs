namespace CrushIt.API
{
    public static class ApiInitializer
    {
        private static IApiClient? _apiClient;
        private static AntiCheatService? _antiCheatService;
        private static ApiConfiguration? _currentConfig;
        private static ApiClient? _concreteApiClient;

        public static void Initialize(ApiConfiguration configuration)
        {
            _currentConfig = configuration;
            
            // Use MockApiClient for local development if UseMockApi is enabled
            if (configuration.UseMockApi)
            {
                _apiClient = new MockApiClient(configuration.EnableLogging);
                _concreteApiClient = null;
                
                if (configuration.EnableLogging)
                {
                    Console.WriteLine("API initialized with MockApiClient (local development mode)");
                    Console.WriteLine($"Validation enabled: {configuration.EnableValidation}");
                    Console.WriteLine($"Cache enabled: {configuration.EnableCache}");
                    Console.WriteLine($"Analytics enabled: {configuration.EnableAnalytics}");
                }
            }
            else
            {
                _concreteApiClient = new ApiClient(configuration.BaseUrl, configuration.ApiKey);
                _apiClient = _concreteApiClient;

                if (configuration.EnableLogging)
                {
                    Console.WriteLine($"API initialized with base URL: {configuration.BaseUrl}");
                    Console.WriteLine($"Validation enabled: {configuration.EnableValidation}");
                    Console.WriteLine($"Cache enabled: {configuration.EnableCache}");
                    Console.WriteLine($"Analytics enabled: {configuration.EnableAnalytics}");
                }
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

        public static ApiCache? GetCache()
        {
            // MockApiClient doesn't have cache, return null in mock mode
            return _concreteApiClient?.GetCache();
        }

        public static RateLimiter? GetRateLimiter()
        {
            // MockApiClient doesn't have rate limiter, return null in mock mode
            return _concreteApiClient?.GetRateLimiter();
        }
    }
}