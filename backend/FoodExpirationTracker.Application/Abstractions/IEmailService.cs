namespace FoodExpirationTracker.Application.Abstractions;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string code);
}
