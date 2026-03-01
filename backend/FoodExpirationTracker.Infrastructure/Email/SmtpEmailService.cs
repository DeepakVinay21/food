using System.Net;
using System.Net.Mail;
using FoodExpirationTracker.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FoodExpirationTracker.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly string _senderEmail;
    private readonly string _appPassword;

    public SmtpEmailService(ILogger<SmtpEmailService> logger)
    {
        _logger = logger;
        _senderEmail = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? "";
        _appPassword = Environment.GetEnvironmentVariable("SMTP_APP_PASSWORD") ?? "";

        if (string.IsNullOrEmpty(_senderEmail) || string.IsNullOrEmpty(_appPassword))
        {
            _logger.LogWarning("SMTP_SENDER_EMAIL or SMTP_APP_PASSWORD not set. Email sending will fail at runtime.");
        }
    }

    public async Task SendVerificationEmailAsync(string toEmail, string code)
    {
        try
        {
            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_senderEmail, _appPassword),
                EnableSsl = true,
            };

            var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, "FoodTracker"),
                Subject = $"Your verification code: {code}",
                IsBodyHtml = true,
                Body = BuildHtmlBody(code),
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("Verification email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendExpiryAlertEmailAsync(string toEmail, string productName, int daysUntilExpiry)
    {
        try
        {
            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_senderEmail, _appPassword),
                EnableSsl = true,
            };

            var urgency = daysUntilExpiry <= 0 ? "has expired" : daysUntilExpiry == 1 ? "expires tomorrow" : $"expires in {daysUntilExpiry} days";
            var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, "FoodTracker"),
                Subject = $"Expiry Alert: {productName} {urgency}!",
                IsBodyHtml = true,
                Body = BuildExpiryHtmlBody(productName, daysUntilExpiry, urgency),
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("Expiry alert email sent to {Email} for {Product}", toEmail, productName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send expiry alert email to {Email}", toEmail);
        }
    }

    private static string BuildHtmlBody(string code)
    {
        return $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px; background: #f8fafc; border-radius: 16px;">
                <h2 style="color: #1e293b; margin-bottom: 8px;">FoodTracker</h2>
                <p style="color: #475569; font-size: 15px;">Your email verification code is:</p>
                <div style="background: #ffffff; border: 2px solid #e2e8f0; border-radius: 12px; padding: 24px; text-align: center; margin: 24px 0;">
                    <span style="font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #0f172a;">{code}</span>
                </div>
                <p style="color: #64748b; font-size: 13px;">This code expires in 5 minutes. If you didn't request this, ignore this email.</p>
            </div>
            """;
    }

    private static string BuildExpiryHtmlBody(string productName, int daysUntilExpiry, string urgency)
    {
        var color = daysUntilExpiry <= 1 ? "#ef4444" : "#f97316";
        var emoji = daysUntilExpiry <= 0 ? "&#x1F6A8;" : daysUntilExpiry <= 1 ? "&#x26A0;&#xFE0F;" : "&#x23F0;";
        return $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px; background: #f8fafc; border-radius: 16px;">
                <h2 style="color: #1e293b; margin-bottom: 8px;">FoodTracker {emoji}</h2>
                <p style="color: #475569; font-size: 15px;">Expiry alert for an item in your pantry:</p>
                <div style="background: #ffffff; border: 2px solid {color}33; border-radius: 12px; padding: 24px; text-align: center; margin: 24px 0;">
                    <p style="font-size: 24px; font-weight: bold; color: #0f172a; margin: 0 0 8px 0;">{productName}</p>
                    <p style="font-size: 16px; color: {color}; font-weight: 600; margin: 0;">{urgency}</p>
                </div>
                <p style="color: #64748b; font-size: 13px;">Open FoodTracker to use it before it goes to waste!</p>
            </div>
            """;
    }
}
