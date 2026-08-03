namespace CrushIt.API
{
    public class ApiConfiguration
    {
        public string BaseUrl { get; set; } = "https://api.crushit-game.com/v1";
        public string ApiKey { get; set; } = "your-api-key-here";
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableLogging { get; set; } = true;
        public bool EnableValidation { get; set; } = true;

        public static ApiConfiguration Default => new ApiConfiguration();
    }
}