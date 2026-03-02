using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using FoodExpirationTracker.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FoodExpirationTracker.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly string _senderEmail;
    private readonly string? _resendApiKey;
    private readonly string? _smtpAppPassword;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly bool _useResend;
    private static readonly HttpClient HttpClient = new();

    public SmtpEmailService(ILogger<SmtpEmailService> logger)
    {
        _logger = logger;
        _resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
        _senderEmail = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? "onboarding@resend.dev";
        _smtpAppPassword = Environment.GetEnvironmentVariable("SMTP_APP_PASSWORD");
        _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com";
        _smtpPort = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : 587;

        _useResend = !string.IsNullOrEmpty(_resendApiKey);

        if (_useResend)
        {
            _logger.LogInformation("Using Resend HTTP API for email delivery.");
        }
        else if (!string.IsNullOrEmpty(_smtpAppPassword))
        {
            _logger.LogInformation("Using SMTP ({Host}:{Port}) for email delivery.", _smtpHost, _smtpPort);
        }
        else
        {
            _logger.LogWarning("No email provider configured. Set RESEND_API_KEY or SMTP_APP_PASSWORD.");
        }
    }

    public async Task SendVerificationEmailAsync(string toEmail, string code)
    {
        var subject = $"Your verification code: {code}";
        var body = BuildHtmlBody(code);

        try
        {
            if (_useResend)
                await SendViaResendAsync(toEmail, subject, body);
            else
                await SendViaSmtpAsync(toEmail, subject, body);

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
        var urgency = daysUntilExpiry <= 0 ? "has expired" : daysUntilExpiry == 1 ? "expires tomorrow" : $"expires in {daysUntilExpiry} days";
        var subject = $"Expiry Alert: {productName} {urgency}!";
        var body = BuildExpiryHtmlBody(productName, daysUntilExpiry, urgency);

        try
        {
            if (_useResend)
                await SendViaResendAsync(toEmail, subject, body);
            else
                await SendViaSmtpAsync(toEmail, subject, body);

            _logger.LogInformation("Expiry alert email sent to {Email} for {Product}", toEmail, productName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send expiry alert email to {Email}", toEmail);
            throw;
        }
    }

    private async Task SendViaResendAsync(string toEmail, string subject, string htmlBody)
    {
        var payload = JsonSerializer.Serialize(new
        {
            from = $"Pantry AI <{_senderEmail}>",
            to = new[] { toEmail },
            subject,
            html = htmlBody,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resendApiKey);

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Resend API error ({response.StatusCode}): {error}");
        }
    }

    private async Task SendViaSmtpAsync(string toEmail, string subject, string htmlBody)
    {
        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            Credentials = new NetworkCredential(_senderEmail, _smtpAppPassword),
            EnableSsl = true,
        };

        var message = new MailMessage
        {
            From = new MailAddress(_senderEmail, "Pantry AI"),
            Subject = subject,
            IsBodyHtml = true,
            Body = htmlBody,
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }

    private static string BuildHtmlBody(string code)
    {
        return $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px; background: #f8fafc; border-radius: 16px;">
                <h2 style="color: #1e293b; margin-bottom: 8px;">Pantry AI</h2>
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
                <h2 style="color: #1e293b; margin-bottom: 8px;">Pantry AI {emoji}</h2>
                <p style="color: #475569; font-size: 15px;">Expiry alert for an item in your pantry:</p>
                <div style="background: #ffffff; border: 2px solid {color}33; border-radius: 12px; padding: 24px; text-align: center; margin: 24px 0;">
                    <p style="font-size: 24px; font-weight: bold; color: #0f172a; margin: 0 0 8px 0;">{productName}</p>
                    <p style="font-size: 16px; color: {color}; font-weight: 600; margin: 0;">{urgency}</p>
                </div>
                <p style="color: #64748b; font-size: 13px;">Open Pantry AI to use it before it goes to waste!</p>
            </div>
            """;
    }
}
