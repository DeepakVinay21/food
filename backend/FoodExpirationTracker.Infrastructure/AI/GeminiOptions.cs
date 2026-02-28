namespace FoodExpirationTracker.Infrastructure.AI;

public class GeminiOptions
{
    public bool Enabled { get; set; } = true;
    public string Model { get; set; } = "gemini-1.5-flash";
    public string ApiKey { get; set; } = string.Empty;
}

