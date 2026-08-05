namespace CrushIt.API
{
    public class ApiConfiguration
    {
        public string BaseUrl { get; set; } = "https://api.crushit-game.com/v1";
        public string ApiKey { get; set; } = "your-api-key-here";
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableLogging { get; set; } = true;
        public bool EnableValidation { get; set; } = true;
        public bool UseMockApi { get; set; } = true; // Use mock API for local development
        
        // Retry settings
        public int MaxRetries { get; set; } = 3;
        public double RetryBackoffMultiplier { get; set; } = 2.0;
        
        // Rate limiting settings
        public int RateLimitMaxRequests { get; set; } = 100;
        public int RateLimitTimeWindowMinutes { get; set; } = 1;
        
        // Cache settings
        public bool EnableCache { get; set; } = true;
        public int CacheTtlMinutes { get; set; } = 5;
        public int MaxCacheSize { get; set; } = 1000;
        
        // Analytics settings
        public bool EnableAnalytics { get; set; } = true;
        public bool EnableErrorReporting { get; set; } = true;
        public bool EnableUsageStats { get; set; } = true;

        public static ApiConfiguration Default => new ApiConfiguration();
    }
}