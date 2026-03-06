namespace MV.DomainLayer.Configuration
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-2.0-flash";
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 1000;
    }
}
