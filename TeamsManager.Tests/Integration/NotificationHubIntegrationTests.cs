using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace TeamsManager.Tests.Integration
{
    /// <summary>
    /// Podstawowe testy weryfikujące konfigurację SignalR
    /// Pełne testy integracyjne wymagają dostępu do Program class
    /// </summary>
    public class NotificationHubIntegrationTests : IAsyncDisposable
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _httpClient;
        private readonly HubConnection _hubConnection;
        private readonly string _testToken;

        public NotificationHubIntegrationTests()
        {
            _factory = new TestWebApplicationFactory();
            _httpClient = _factory.CreateClient();
            _testToken = GenerateTestJwtToken();
            
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_httpClient.BaseAddress}notificationHub", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("Authorization", $"Bearer {_testToken}");
                })
                .Build();
        }

        [Fact]
        public async Task SignalRHub_ShouldAcceptConnectionWithValidJwtToken()
        {
            // Arrange
            var notificationReceived = false;
            var tcs = new TaskCompletionSource<bool>();

            _hubConnection.On<object>("ReceiveNotification", notification =>
            {
                notificationReceived = true;
                tcs.SetResult(true);
            });

            // Act
            await _hubConnection.StartAsync();
            
            // Assert
            _hubConnection.State.Should().Be(HubConnectionState.Connected);
            
            // Czekaj na powiadomienie powitalne (lub timeout)
            var received = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;
            
            // Sprawdź że połączenie się udało (nawet jeśli nie ma powiadomienia powitalnego)
            _hubConnection.State.Should().Be(HubConnectionState.Connected);
        }

        [Fact]
        public async Task SignalRHub_ShouldRejectConnectionWithoutToken()
        {
            // Arrange
            var unauthorizedConnection = new HubConnectionBuilder()
                .WithUrl($"{_httpClient.BaseAddress}notificationHub", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    // Brak tokenu!
                })
                .Build();

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await unauthorizedConnection.StartAsync();
            });
        }

        [Fact]
        public async Task SignalRHub_ShouldHandleTokenFromQueryString()
        {
            // Arrange
            var queryStringConnection = new HubConnectionBuilder()
                .WithUrl($"{_httpClient.BaseAddress}notificationHub?access_token={_testToken}", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    // Token w query string zamiast w nagłówku
                })
                .Build();

            // Act
            await queryStringConnection.StartAsync();

            // Assert
            queryStringConnection.State.Should().Be(HubConnectionState.Connected);
            await queryStringConnection.DisposeAsync();
        }

        private string GenerateTestJwtToken()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebApplicationFactory.TestSecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim("upn", "test@example.com"),
                new Claim("preferred_username", "test@example.com"),
                new Claim("oid", "12345678-1234-1234-1234-123456789012"),
                new Claim("tid", TestWebApplicationFactory.TestTenantId),
                new Claim(ClaimTypes.Role, "Administrator")
            };

            var token = new JwtSecurityToken(
                issuer: TestWebApplicationFactory.TestIssuer,
                audience: TestWebApplicationFactory.TestAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
            _httpClient?.Dispose();
            _factory?.Dispose();
        }

        /// <summary>
        /// Niestandardowa fabryka testowa która nadpisuje konfigurację JWT
        /// </summary>
        private class TestWebApplicationFactory : WebApplicationFactory<Program>
        {
            public const string TestSecretKey = "test-secret-key-for-integration-tests-must-be-long-enough-for-256-bit";
            public const string TestTenantId = "test-tenant-id";
            public const string TestIssuer = "test-issuer";
            public const string TestAudience = "test-audience";

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                // Najpierw ustaw konfigurację testową PRZED uruchomieniem Program.cs
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Najpierw usuwamy wszystkie poprzednie źródła konfiguracji
                    config.Sources.Clear();
                    
                    // Dodajemy testową konfigurację Azure AD która przejdzie walidację
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                        ["AzureAd:TenantId"] = TestTenantId,
                        ["AzureAd:ClientId"] = "test-client-id",
                        ["AzureAd:ClientSecret"] = "test-client-secret",
                        ["AzureAd:Audience"] = TestAudience,
                        ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                        ["Logging:LogLevel:Default"] = "Error",
                        ["AllowedHosts"] = "*"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Nadpisz konfigurację JWT Bearer dla testów
                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
                        
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = TestIssuer,
                            ValidateAudience = true,
                            ValidAudience = TestAudience,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = key,
                            ClockSkew = TimeSpan.FromMinutes(5),
                            // Wyłącz walidację metadanych (nie łączymy się z Azure AD)
                            RequireSignedTokens = true,
                            RequireExpirationTime = true
                        };

                        // Wyczyść authority żeby nie łączyło się z Azure AD
                        options.Authority = null;
                        options.MetadataAddress = null;
                        options.RequireHttpsMetadata = false;

                        // Zachowaj oryginalny event handler dla query string
                        var originalOnMessageReceived = options.Events?.OnMessageReceived;
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var accessToken = context.Request.Query["access_token"];
                                var path = context.HttpContext.Request.Path;
                                if (!string.IsNullOrEmpty(accessToken) &&
                                    path.StartsWithSegments("/notificationHub"))
                                {
                                    context.Token = accessToken;
                                }
                                return originalOnMessageReceived?.Invoke(context) ?? Task.CompletedTask;
                            },
                            OnAuthenticationFailed = context => 
                            {
                                Console.WriteLine($"[TEST] JWT Auth Failed: {context.Exception.Message}");
                                return Task.CompletedTask;
                            },
                            OnTokenValidated = context => 
                            {
                                Console.WriteLine($"[TEST] JWT Token Validated for: {context.Principal?.Identity?.Name}");
                                return Task.CompletedTask;
                            }
                        };
                    });
                });

                // Ustaw środowisko na Test
                builder.UseEnvironment("Test");
            }
        }
    }
} 