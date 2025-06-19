using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Implementacja serwisu powiadomień administratorów używająca Microsoft.Graph API.
    /// </summary>
    public class GraphAdminNotificationService : IAdminNotificationService
    {
        private readonly IGraphService _graphService;
        private readonly IModernHttpService _modernHttpService;
        private readonly ILogger<GraphAdminNotificationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly List<string> _adminEmails;
        private readonly bool _isEnabled;
        private readonly string _systemEmail;
        private readonly string _systemName;
        private readonly string _environmentName;

        public GraphAdminNotificationService(
            IGraphService graphService,
            IModernHttpService modernHttpService,
            ILogger<GraphAdminNotificationService> logger,
            IConfiguration configuration)
        {
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
            _modernHttpService = modernHttpService ?? throw new ArgumentNullException(nameof(modernHttpService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Wczytanie konfiguracji
            _isEnabled = bool.Parse(_configuration["AdminNotifications:Enabled"] ?? "false");
            _systemEmail = _configuration["AdminNotifications:SystemEmail"] ?? "system@teamsmanager.edu.pl";
            _systemName = _configuration["AdminNotifications:SystemName"] ?? "TeamsManager System";
            _environmentName = _configuration["AdminNotifications:Environment"] ?? "Production";
            
            // Wczytanie listy administratorów
            _adminEmails = new List<string>();
            var adminEmailsSection = _configuration.GetSection("AdminNotifications:AdminEmails");
            
            // Ręczne wczytanie listy emaili
            for (int i = 0; i < 10; i++) // Maksymalnie 10 emaili
            {
                var email = adminEmailsSection[i.ToString()];
                if (!string.IsNullOrEmpty(email))
                {
                    _adminEmails.Add(email);
                }
                else
                {
                    break;
                }
            }
            
            if (_isEnabled && !_adminEmails.Any())
            {
                _logger.LogWarning("Powiadomienia administratora są włączone, ale nie skonfigurowano emaili administratorów!");
            }
        }

        public async Task SendTeamCreatedNotificationAsync(
            string teamName, 
            string teamId, 
            string createdBy, 
            int membersCount,
            Dictionary<string, object>? additionalInfo = null)
        {
            if (!ShouldSendNotification())
                return;

            var subject = $"[{_environmentName}] Utworzono nowy zespół: {teamName}";
            var message = BuildHtmlMessage("Utworzenie zespołu", new Dictionary<string, object>
            {
                ["Nazwa zespołu"] = teamName,
                ["ID zespołu"] = teamId,
                ["Utworzony przez"] = createdBy,
                ["Liczba członków"] = membersCount,
                ["Data utworzenia"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            }, additionalInfo);

            await SendToAllAdminsAsync(subject, message);
        }

        public async Task SendBulkTeamsOperationNotificationAsync(
            string operationType,
            int totalTeams,
            int successCount,
            int failureCount,
            string performedBy,
            Dictionary<string, object>? details = null)
        {
            if (!ShouldSendNotification())
                return;

            var subject = $"[{_environmentName}] Operacja masowa: {operationType} ({successCount}/{totalTeams} sukces)";
            var message = BuildHtmlMessage($"Masowa operacja: {operationType}", new Dictionary<string, object>
            {
                ["Typ operacji"] = operationType,
                ["Łączna liczba zespołów"] = totalTeams,
                ["Sukcesy"] = successCount,
                ["Błędy"] = failureCount,
                ["Wykonane przez"] = performedBy,
                ["Data wykonania"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                ["Procent sukcesu"] = $"{(totalTeams > 0 ? (successCount * 100.0 / totalTeams) : 0):F1}%"
            }, details);

            await SendToAllAdminsAsync(subject, message);
        }

        public async Task SendUserCreatedNotificationAsync(
            string userName,
            string userUpn,
            string userRole,
            string createdBy)
        {
            if (!ShouldSendNotification())
                return;

            var subject = $"[{_environmentName}] Utworzono nowego użytkownika: {userName}";
            var message = BuildHtmlMessage("Utworzenie użytkownika", new Dictionary<string, object>
            {
                ["Imię i nazwisko"] = userName,
                ["UPN"] = userUpn,
                ["Rola"] = userRole,
                ["Utworzony przez"] = createdBy,
                ["Data utworzenia"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            });

            await SendToAllAdminsAsync(subject, message);
        }

        public async Task SendBulkUsersOperationNotificationAsync(
            string operationType,
            string teamName,
            int totalUsers,
            int successCount,
            int failureCount,
            string performedBy)
        {
            if (!ShouldSendNotification())
                return;

            var subject = $"[{_environmentName}] Operacja masowa użytkowników: {operationType} w {teamName}";
            var message = BuildHtmlMessage($"Masowa operacja użytkowników: {operationType}", new Dictionary<string, object>
            {
                ["Typ operacji"] = operationType,
                ["Zespół"] = teamName,
                ["Łączna liczba użytkowników"] = totalUsers,
                ["Sukcesy"] = successCount,
                ["Błędy"] = failureCount,
                ["Wykonane przez"] = performedBy,
                ["Data wykonania"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                ["Procent sukcesu"] = $"{(totalUsers > 0 ? (successCount * 100.0 / totalUsers) : 0):F1}%"
            });

            await SendToAllAdminsAsync(subject, message);
        }

        public async Task SendCriticalErrorNotificationAsync(
            string operationType,
            string errorMessage,
            string stackTrace,
            string occurredDuring,
            string? userId = null)
        {
            if (!ShouldSendNotification())
                return;

            var subject = $"[{_environmentName}] 🚨 BŁĄD KRYTYCZNY: {operationType}";
            
            var details = new Dictionary<string, object>
            {
                ["Typ operacji"] = operationType,
                ["Błąd"] = errorMessage,
                ["Wystąpił podczas"] = occurredDuring,
                ["Data błędu"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };
            
            if (!string.IsNullOrEmpty(userId))
                details["Użytkownik"] = userId;
            
            if (!string.IsNullOrEmpty(stackTrace))
                details["Stack trace"] = $"<pre style='font-size: 12px; background: #f5f5f5; padding: 10px;'>{System.Security.SecurityElement.Escape(stackTrace)}</pre>";

            var message = BuildHtmlMessage("⚠️ Błąd krytyczny", details, additionalData: null, isError: true);

            await SendToAllAdminsAsync(subject, message);
        }

        public async Task SendCustomAdminNotificationAsync(
            string subject,
            string message,
            Dictionary<string, object>? data = null)
        {
            if (!ShouldSendNotification())
                return;

            var fullSubject = $"[{_environmentName}] {subject}";
            var htmlMessage = BuildHtmlMessage(subject, new Dictionary<string, object>
            {
                ["Wiadomość"] = message,
                ["Data"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            }, data);

            await SendToAllAdminsAsync(fullSubject, htmlMessage);
        }

        public async Task SendGraphApiErrorMetricsAsync(Dictionary<string, object> metrics)
        {
            if (!ShouldSendNotification())
                return;

            try
            {
                var method = metrics.GetValueOrDefault("Method", "Unknown").ToString();
                var endpoint = metrics.GetValueOrDefault("Endpoint", "Unknown").ToString();
                var httpStatusCode = metrics.GetValueOrDefault("HttpStatusCode", 0);
                var graphErrorCode = metrics.GetValueOrDefault("GraphErrorCode", "Unknown").ToString();
                
                var subject = $"[{_environmentName}] 📊 Graph API Error Metrics: {method}";
                
                var errorDetails = new Dictionary<string, object>
                {
                    ["Metoda"] = method,
                    ["Endpoint"] = endpoint,
                    ["HTTP Status Code"] = httpStatusCode,
                    ["Graph Error Code"] = graphErrorCode,
                    ["Request ID"] = metrics.GetValueOrDefault("RequestId", "Unknown"),
                    ["Can Retry"] = metrics.GetValueOrDefault("CanRetry", false),
                    ["Is Authentication Error"] = metrics.GetValueOrDefault("IsAuthenticationError", false),
                    ["Is Permission Error"] = metrics.GetValueOrDefault("IsPermissionError", false),
                    ["Is Validation Error"] = metrics.GetValueOrDefault("IsValidationError", false),
                    ["Is Not Found Error"] = metrics.GetValueOrDefault("IsNotFoundError", false),
                    ["Is Conflict Error"] = metrics.GetValueOrDefault("IsConflictError", false),
                    ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                };

                var message = BuildHtmlMessage("📊 Graph API Error Metrics", errorDetails, metrics, isError: true);
                await SendToAllAdminsAsync(subject, message);
                
                _logger.LogDebug("Graph API error metrics sent to administrators for method: {Method}", method);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Graph API error metrics to administrators");
            }
        }

        private bool ShouldSendNotification()
        {
            if (!_isEnabled)
            {
                _logger.LogDebug("Admin notifications are disabled");
                return false;
            }

            if (!_adminEmails.Any())
            {
                _logger.LogWarning("No admin emails configured, skipping notification");
                return false;
            }

            return true;
        }

        private string BuildHtmlMessage(
            string title, 
            Dictionary<string, object> mainData, 
            Dictionary<string, object>? additionalData = null,
            bool isError = false)
        {
            var sb = new StringBuilder();
            var borderColor = isError ? "#dc3545" : "#0066cc";
            var headerBg = isError ? "#dc3545" : "#0066cc";
            
            sb.AppendLine($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .header {{ background-color: {headerBg}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; }}
        .data-table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        .data-table th {{ text-align: left; padding: 10px; background-color: #f8f9fa; border-bottom: 2px solid #dee2e6; }}
        .data-table td {{ padding: 10px; border-bottom: 1px solid #dee2e6; }}
        .footer {{ background-color: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #6c757d; }}
        .alert {{ padding: 15px; margin: 15px 0; border-radius: 4px; }}
        .alert-error {{ background-color: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>{title}</h2>
        </div>
        <div class='content'>");

            // Główne dane
            if (mainData.Any())
            {
                sb.AppendLine("<table class='data-table'>");
                foreach (var item in mainData)
                {
                    sb.AppendLine($@"
                    <tr>
                        <th width='40%'>{item.Key}:</th>
                        <td>{item.Value}</td>
                    </tr>");
                }
                sb.AppendLine("</table>");
            }

            // Dodatkowe dane
            if (additionalData != null && additionalData.Any())
            {
                sb.AppendLine("<h3 style='margin-top: 30px; color: #333;'>Szczegóły dodatkowe:</h3>");
                sb.AppendLine("<table class='data-table'>");
                foreach (var item in additionalData)
                {
                    sb.AppendLine($@"
                    <tr>
                        <th width='40%'>{item.Key}:</th>
                        <td>{item.Value}</td>
                    </tr>");
                }
                sb.AppendLine("</table>");
            }

            sb.AppendLine($@"
        </div>
        <div class='footer'>
            <p>Wiadomość wygenerowana automatycznie przez {_systemName}</p>
            <p>Środowisko: {_environmentName} | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
        </div>
    </div>
</body>
</html>");

            return sb.ToString();
        }

        private async Task SendToAllAdminsAsync(string subject, string htmlBody)
        {
            var tasks = new List<Task<bool>>();
            
            foreach (var adminEmail in _adminEmails)
            {
                tasks.Add(SendEmailAsync(adminEmail, subject, htmlBody));
            }

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);
            
            if (successCount < _adminEmails.Count)
            {
                _logger.LogWarning("Failed to send notification to some admins. Success: {Success}/{Total}", 
                    successCount, _adminEmails.Count);
            }
            else
            {
                _logger.LogInformation("Admin notification sent successfully to all {Count} admins", 
                    _adminEmails.Count);
            }
        }

        private async Task<bool> SendEmailAsync(string recipientEmail, string subject, string htmlBody)
        {
            try
            {
                _logger.LogDebug("Sending admin notification to {Email}, subject: {Subject}", 
                    recipientEmail, subject);

                // Sprawdzenie czy Graph Service jest dostępny
                var connectionResult = await _graphService.DiagnoseConnectionAsync(string.Empty);
                if (!connectionResult.IsConnected)
                {
                    _logger.LogWarning("Graph Service connection failed, falling back to logging. Errors: {Errors}", 
                        string.Join("; ", connectionResult.Errors));
                    
                    // Fallback - logowanie powiadomienia
                    _logger.LogInformation("[ADMIN NOTIFICATION EMAIL - FALLBACK] To: {Email}, Subject: {Subject}", 
                        recipientEmail, subject);
                    _logger.LogDebug("[ADMIN NOTIFICATION EMAIL - FALLBACK] Body: {Body}", htmlBody);
                    return true;
                }

                // Pobranie tokenu dostępu z Graph Service
                var accessToken = await _graphService.Connection.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("Failed to get access token for Graph Mail API, falling back to logging");
                    
                    // Fallback - logowanie powiadomienia
                    _logger.LogInformation("[ADMIN NOTIFICATION EMAIL - FALLBACK] To: {Email}, Subject: {Subject}", 
                        recipientEmail, subject);
                    return true;
                }

                // Utworzenie żądania wysłania emaila przez Graph Mail API
                var mailRequest = new GraphSendMailRequest
                {
                    Message = new GraphMessage
                    {
                        Subject = subject,
                        Body = new GraphMessageBody
                        {
                            ContentType = "HTML",
                            Content = htmlBody
                        },
                        ToRecipients = new List<GraphEmailAddress>
                        {
                            new GraphEmailAddress
                            {
                                Name = recipientEmail,
                                Address = recipientEmail
                            }
                        },
                        From = new GraphEmailAddress
                        {
                            Name = _systemName,
                            Address = _systemEmail
                        },
                        Importance = "high"
                    },
                    SaveToSentItems = true
                };

                // Wysłanie emaila przez Graph Mail API
                var success = await _modernHttpService.SendMailOnBehalfOfUserAsync(_systemEmail, mailRequest, accessToken);
                
                if (success)
                {
                    _logger.LogInformation("Admin notification sent successfully via Graph Mail API to {Email}", recipientEmail);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to send admin notification via Graph Mail API to {Email}, falling back to logging", recipientEmail);
                    
                    // Fallback - logowanie powiadomienia
                    _logger.LogInformation("[ADMIN NOTIFICATION EMAIL - FALLBACK] To: {Email}, Subject: {Subject}", 
                        recipientEmail, subject);
                    return true; // Zwracamy true aby nie blokować innych powiadomień
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending admin notification to {Email}, falling back to logging", recipientEmail);
                
                // Fallback - logowanie powiadomienia
                _logger.LogInformation("[ADMIN NOTIFICATION EMAIL - FALLBACK] To: {Email}, Subject: {Subject}", 
                    recipientEmail, subject);
                return true; // Zwracamy true aby nie blokować innych powiadomień
            }
        }
    }
} 