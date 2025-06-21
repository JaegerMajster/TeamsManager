using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.IO;
using System.Net.Http;
using TeamsManager.Core.Extensions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.Core.Models.Configuration;
using TeamsManager.Data;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Data.Repositories;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Services.Synchronization;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Services;
using TeamsManager.Application.Services;
using TeamsManager.Core.Services.Graph;
using TeamsManager.Core.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TeamsManager.Core.Models;
using TeamsManager.Core.Common;
using TeamsManager.Core.Services.Cache;
using TeamsManager.Core.Services.UserContext;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Data.UnitOfWork;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Microsoft.Extensions.Caching.Memory;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.Auth;
using Microsoft.AspNetCore.Routing;
using TeamsManager.Core.Abstractions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Http;
using TeamsManager.UI.Services.Auth;
using TeamsManager.UI.Middleware;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Embedded API Server dla TeamsManager
    /// Uruchamia lokalny serwer API wewnątrz aplikacji UI
    /// </summary>
    public class EmbeddedApiServer : IDisposable
    {
        private readonly ILogger<EmbeddedApiServer> _logger;
        private IHost? _host;
        private int _httpsPort;
        private int _httpPort;
        private bool _isRunning;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // Przechowywanie konfiguracji załadowanej asynchronicznie
        private TeamsManager.Core.Models.Configuration.AzureAdConfiguration? _azureAdConfig;
        private TeamsManager.Core.Models.Configuration.ApplicationConfiguration? _applicationConfig;

        public int HttpsPort => _httpsPort;
        public int HttpPort => _httpPort;
        public bool IsRunning => _isRunning && _host != null && !_cancellationTokenSource.Token.IsCancellationRequested;
        public string BaseUrl => $"https://localhost:{_httpsPort}";

        public EmbeddedApiServer(ILogger<EmbeddedApiServer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cancellationTokenSource = new CancellationTokenSource();
            
            _logger.LogInformation("🔧 [FACTORY] Tworzenie EmbeddedApiServer - konfiguracja będzie ładowana asynchronicznie");
        }

        /// <summary>
        /// Ustawia konfigurację dla EmbeddedApiServer
        /// </summary>
        public void SetConfiguration(
            TeamsManager.Core.Models.Configuration.AzureAdConfiguration? azureAdConfig,
            TeamsManager.Core.Models.Configuration.ApplicationConfiguration? applicationConfig)
        {
            _logger.LogInformation("[EMBEDDED-API] 🔧 SetConfiguration wywołane...");
            
            _logger.LogInformation("[EMBEDDED-API] 🔧 Parametr azureAdConfig: {AzureAdConfig}", 
                azureAdConfig == null ? "NULL" : $"ClientId={azureAdConfig.Api?.ClientId}, IsValid={azureAdConfig.Api?.IsValid()}");
            
            _logger.LogInformation("[EMBEDDED-API] 🔧 Parametr applicationConfig: {ApplicationConfig}", 
                applicationConfig == null ? "NULL" : $"Name={applicationConfig.ApplicationName}, IsValid={applicationConfig.IsValid()}");
            
            _azureAdConfig = azureAdConfig;
            _applicationConfig = applicationConfig;
            
            _logger.LogInformation("[EMBEDDED-API] ✅ Konfiguracja ustawiona - Azure AD: {AzureAdSet}, Application: {ApplicationSet}", 
                _azureAdConfig != null, _applicationConfig != null);
        }

        /// <summary>
        /// Sprawdza i waliduje konfigurację przed uruchomieniem
        /// </summary>
        private void ValidateConfiguration()
        {
            _logger.LogInformation("[EMBEDDED-API] 🔧 Sprawdzanie konfiguracji przekazanej przez SetConfiguration...");
            
            if (_azureAdConfig?.Api?.IsValid() != true)
            {
                _logger.LogError("[EMBEDDED-API] ❌ Brak prawidłowej konfiguracji Azure AD!");
                _logger.LogError("[EMBEDDED-API] ❌ _azureAdConfig: {Config}", 
                    _azureAdConfig == null ? "NULL" : $"ClientId={_azureAdConfig.Api?.ClientId}, IsValid={_azureAdConfig.Api?.IsValid()}");
                throw new InvalidOperationException("Brak konfiguracji Azure AD. Wywołaj SetConfiguration przed StartAsync.");
            }
            
            // ===== SZCZEGÓŁOWE LOGOWANIE KONFIGURACJI AZURE AD =====
            _logger.LogInformation("[EMBEDDED-API] 🔍 SZCZEGÓŁY KONFIGURACJI Azure AD:");
            _logger.LogInformation("[EMBEDDED-API] 🔍   - ClientId: {ClientId}", _azureAdConfig.Api?.ClientId);
            _logger.LogInformation("[EMBEDDED-API] 🔍   - ClientSecret: {ClientSecret}", 
                string.IsNullOrEmpty(_azureAdConfig.Api?.ClientSecret) ? "NOT SET" : "SET");
            _logger.LogInformation("[EMBEDDED-API] 🔍   - ClientSecret Length: {Length}", _azureAdConfig.Api?.ClientSecret?.Length ?? 0);
            _logger.LogInformation("[EMBEDDED-API] 🔍   - Audience: {Audience}", _azureAdConfig.Api?.Audience);
            _logger.LogInformation("[EMBEDDED-API] 🔍   - ApiScope: {ApiScope}", _azureAdConfig.Api?.ApiScope);
            _logger.LogInformation("[EMBEDDED-API] 🔍   - IsValid(): {IsValid}", _azureAdConfig.Api?.IsValid());
            
            if (_applicationConfig?.IsValid() != true)
            {
                _logger.LogWarning("[EMBEDDED-API] ⚠️ Brak prawidłowej konfiguracji aplikacji - używam domyślnej");
                _applicationConfig = new TeamsManager.Core.Models.Configuration.ApplicationConfiguration
                {
                    ApplicationName = "TeamsManager",
                    ApplicationVersion = "2.0.0",
                    Environment = "Development",
                    ConnectionStrings = new TeamsManager.Core.Models.Configuration.ConnectionStringsSettings
                    {
                        DefaultConnection = "Data Source=teamsmanager_embedded.db"
                    }
                };
            }
            
            _logger.LogInformation("[EMBEDDED-API] ✅ Konfiguracja Azure AD: ClientId={ClientId}, Audience={Audience}, ApiScope={ApiScope}", 
                _azureAdConfig.Api?.ClientId, _azureAdConfig.Api?.Audience, _azureAdConfig.Api?.ApiScope);
            _logger.LogInformation("[EMBEDDED-API] ✅ Konfiguracja aplikacji: Name={Name}, Environment={Environment}", 
                _applicationConfig.ApplicationName, _applicationConfig.Environment);
        }

        /// <summary>
        /// Uruchamia embedded API server
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                // Sprawdź czy server już nie jest uruchomiony
                if (IsRunning)
                {
                    _logger.LogInformation("🔄 Embedded API Server już działa na {BaseUrl}", BaseUrl);
                    return true;
                }

                _logger.LogInformation("🚀 Uruchamianie Embedded API Server...");

                // 0. Sprawdź i waliduj konfigurację
                ValidateConfiguration();

                // 1. Znajdź dostępne porty
                _httpsPort = FindAvailablePort(7037, 7100);
                _httpPort = FindAvailablePort(5182, 5200);

                _logger.LogInformation("📡 Wybrane porty: HTTPS={HttpsPort}, HTTP={HttpPort}", _httpsPort, _httpPort);

                // 2. Przygotuj certyfikat SSL
                await EnsureDevelopmentCertificateAsync();

                // 3. Skonfiguruj i uruchom host
                _logger.LogInformation("🔧 Tworzenie hosta...");
                _host = CreateHostBuilder().Build();
                
                _logger.LogInformation("🚀 Uruchamianie hosta...");
                await _host.StartAsync(_cancellationTokenSource.Token);
                _isRunning = true;

                _logger.LogInformation("✅ Embedded API Server uruchomiony pomyślnie na {BaseUrl}", BaseUrl);
                
                // Sprawdź czy host faktycznie działa
                var isHealthy = await HealthCheckAsync();
                _logger.LogInformation("🏥 Health check: {IsHealthy}", isHealthy);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Błąd podczas uruchamiania Embedded API Server: {Message}", ex.Message);
                _logger.LogError("❌ StackTrace: {StackTrace}", ex.StackTrace);
                _logger.LogError("❌ InnerException: {InnerException}", ex.InnerException?.Message ?? "BRAK");
                _isRunning = false;
                return false;
            }
        }

        /// <summary>
        /// Zatrzymuje embedded API server
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_host != null && _isRunning)
                {
                    _logger.LogInformation("🛑 Zatrzymywanie Embedded API Server...");
                    
                    _cancellationTokenSource.Cancel();
                    await _host.StopAsync(TimeSpan.FromSeconds(5));
                    
                    _isRunning = false;
                    _logger.LogInformation("✅ Embedded API Server zatrzymany");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Błąd podczas zatrzymywania Embedded API Server");
            }
        }

        /// <summary>
        /// Sprawdza czy server jest żywy
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                if (!_isRunning) return false;

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync($"{BaseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseKestrel(options =>
                        {
                            // HTTPS
                            options.Listen(IPAddress.Loopback, _httpsPort, listenOptions =>
                            {
                                listenOptions.UseHttps();
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                            });
                            
                            // HTTP (fallback)
                            options.Listen(IPAddress.Loopback, _httpPort, listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                            });
                        })
                        .ConfigureServices(services =>
                        {
                            // Tutaj dodamy wszystkie serwisy z TeamsManager.Api
                            ConfigureApiServices(services);
                        })
                        .Configure(app =>
                        {
                            // Tutaj skonfigurujemy middleware z TeamsManager.Api
                            ConfigureApiPipeline(app);
                        })
                        .UseContentRoot(AppDomain.CurrentDomain.BaseDirectory)
                        .SuppressStatusMessages(true); // Ukryj komunikaty startowe
                })
                .ConfigureLogging(logging =>
                {
                    // ✅ DODAJ SZCZEGÓŁOWE LOGOWANIE (podobnie jak w external API)
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information); // ✅ Pełna kwalifikacja
                    logging.AddConsole();
                    logging.AddDebug();
                    
                    // Dodaj TeamsManager diagnostic logging
                    logging.AddTeamsManagerDiagnosticLogging("EmbeddedApiServer");
                });
        }

        private void ConfigureApiServices(IServiceCollection services)
        {
            try
            {
                _logger.LogInformation("[EMBEDDED-API] 🔧 ===== ROZPOCZĘCIE KONFIGURACJI SERWISÓW =====");
                _logger.LogInformation("[EMBEDDED-API] 🔧 Stan konfiguracji:");
                _logger.LogInformation("[EMBEDDED-API] 🔧   - _azureAdConfig: {AzureAdConfig}", 
                    _azureAdConfig == null ? "NULL" : $"ClientId={_azureAdConfig.Api?.ClientId}, IsValid={_azureAdConfig.Api?.IsValid()}");
                _logger.LogInformation("[EMBEDDED-API] 🔧   - _applicationConfig: {ApplicationConfig}", 
                    _applicationConfig == null ? "NULL" : $"Name={_applicationConfig.ApplicationName}, IsValid={_applicationConfig.IsValid()}");
                
                // ✅ PEŁNA KONFIGURACJA SERWISÓW DLA EMBEDDED MODE
                
                // ===== KONTROLERY - TYLKO DIAGNOSTICS =====
                // BEZPIECZNE: Ładuj tylko DiagnosticsController bez API versioning
                _logger.LogInformation("[EMBEDDED-API] 🔧 Rejestracja kontrolerów...");
                services.AddControllers(options =>
                {
                    // Skonfiguruj podstawowe opcje kontrolerów
                    options.SuppressAsyncSuffixInActionNames = false;
                });
            
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            
            // ===== CONFIGURATION =====
            // Dodaj pustą konfigurację jako fallback dla TokenManager
            var configurationBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Information"
            });
            var configuration = configurationBuilder.Build();
            services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
            _logger.LogInformation("[EMBEDDED-API] ✅ Zarejestrowano IConfiguration");
            
            // ===== KONFIGURACJA PRZEKAZANA PRZEZ SetConfiguration =====
            // Używamy konfiguracji przekazanej wcześniej przez SetConfiguration()
            if (_azureAdConfig == null || _applicationConfig == null)
            {
                _logger.LogError("[EMBEDDED-API] ❌ Brak konfiguracji w ConfigureApiServices!");
                _logger.LogError("[EMBEDDED-API] ❌   - _azureAdConfig: {AzureAdConfig}", _azureAdConfig == null ? "NULL" : "SET");
                _logger.LogError("[EMBEDDED-API] ❌   - _applicationConfig: {ApplicationConfig}", _applicationConfig == null ? "NULL" : "SET");
                throw new InvalidOperationException("Konfiguracja nie została ustawiona! Wywołaj SetConfiguration() przed StartAsync()");
            }

            services.AddSingleton(_azureAdConfig);
            services.AddSingleton(_applicationConfig);
            _logger.LogInformation("[EMBEDDED-API] ✅ Zarejestrowano załadowaną konfigurację Azure AD i aplikacji");
            
            // ===== DATABASE CONTEXT =====
            string connectionString = _applicationConfig?.ConnectionStrings?.DefaultConnection ?? "Data Source=teamsmanager_embedded.db";
            
            services.AddDbContext<TeamsManagerDbContext>(options =>
                options.UseSqlite(connectionString));
            
            // ===== CORE SERVICES =====
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            
            // ===== REPOZYTORIA =====
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<ITeamTemplateRepository, TeamTemplateRepository>();
            services.AddScoped<ISchoolYearRepository, SchoolYearRepository>();
            services.AddScoped<IOperationHistoryRepository, OperationHistoryRepository>();
            services.AddScoped<IApplicationSettingRepository, ApplicationSettingRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<IGenericRepository<SchoolType>, GenericRepository<SchoolType>>();
            services.AddScoped<IGenericRepository<Department>, GenericRepository<Department>>();
            services.AddScoped<IGenericRepository<UserSchoolType>, GenericRepository<UserSchoolType>>();
            services.AddScoped<IGenericRepository<UserSubject>, GenericRepository<UserSubject>>();
            
            // ===== NOTIFICATION SERVICES =====
            // Zawsze używamy StubNotificationService w embedded mode
            services.AddScoped<INotificationService, StubNotificationService>();
            services.AddScoped<IAdminNotificationService, StubAdminNotificationService>();
            
            // ===== SYNCHRONIZATORY =====
            services.AddScoped<IGraphSynchronizer<Team, GraphTeam>, TeamSynchronizer>();
            services.AddScoped<IGraphSynchronizer<User, GraphUser>, UserSynchronizer>();
            services.AddScoped<IGraphSynchronizer<Channel, GraphChannel>, ChannelSynchronizer>();
            
            // ===== CACHE SERVICES =====
            services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
            
            // ===== HTTP SERVICES =====
            // Rejestracja HttpClient dla ModernHttpService
            services.AddHttpClient("MicrosoftGraph", client =>
            {
                client.BaseAddress = new Uri("https://graph.microsoft.com/");
                client.DefaultRequestHeaders.Add("User-Agent", "TeamsManager/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            
            services.AddHttpClient("ExternalApis");
            

            services.AddSingleton<ModernCircuitBreaker>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<ModernCircuitBreaker>>();
                return new ModernCircuitBreaker(
                    failureThreshold: 5,
                    openDuration: TimeSpan.FromMinutes(1),
                    logger: logger
                );
            });
            
            // ===== MSAL CONFIGURATION =====
            _logger.LogInformation("[EMBEDDED-API] Sprawdzanie konfiguracji Azure AD...");
            _logger.LogInformation("[EMBEDDED-API] _azureAdConfig is null: {IsNull}", _azureAdConfig == null);
            if (_azureAdConfig != null)
            {
                _logger.LogInformation("[EMBEDDED-API] _azureAdConfig.Api is null: {IsNull}", _azureAdConfig.Api == null);
                if (_azureAdConfig.Api != null)
                {
                    _logger.LogInformation("[EMBEDDED-API] ClientId: {ClientId}", string.IsNullOrEmpty(_azureAdConfig.Api.ClientId) ? "EMPTY" : "SET");
                    _logger.LogInformation("[EMBEDDED-API] ClientSecret: {ClientSecret}", string.IsNullOrEmpty(_azureAdConfig.Api.ClientSecret) ? "EMPTY" : "SET");
                    _logger.LogInformation("[EMBEDDED-API] Audience: {Audience}", string.IsNullOrEmpty(_azureAdConfig.Api.Audience) ? "EMPTY" : "SET");
                    _logger.LogInformation("[EMBEDDED-API] ApiScope: {ApiScope}", string.IsNullOrEmpty(_azureAdConfig.Api.ApiScope) ? "EMPTY" : "SET");
                    _logger.LogInformation("[EMBEDDED-API] IsValid(): {IsValid}", _azureAdConfig.Api.IsValid());
                }
            }
            
            _logger.LogInformation("[EMBEDDED-API] Sprawdzenie warunków rejestracji IConfidentialClientApplication:");
            _logger.LogInformation("[EMBEDDED-API]   - _azureAdConfig?.Api?.IsValid() == true: {Result}", _azureAdConfig?.Api?.IsValid() == true);
            
            if (_azureAdConfig?.Api?.IsValid() == true)
            {
                _logger.LogInformation("[EMBEDDED-API] ✅ Rejestracja IConfidentialClientApplication...");
                services.AddScoped<IConfidentialClientApplication>(provider =>
                {
                    var authority = $"{_azureAdConfig.Instance?.TrimEnd('/')}/{_azureAdConfig.TenantId}";
                    var logger = provider.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("[EMBEDDED-API] Konfiguracja IConfidentialClientApplication: ClientId='{ApiAppClientId}', Authority='{Authority}', ClientSecret is set: {IsSecretSet}",
                        _azureAdConfig.Api.ClientId,
                        authority,
                        !string.IsNullOrWhiteSpace(_azureAdConfig.Api.ClientSecret));
                        
                    return ConfidentialClientApplicationBuilder.Create(_azureAdConfig.Api.ClientId)
                        .WithClientSecret(_azureAdConfig.Api.ClientSecret)
                        .WithAuthority(new Uri(authority))
                        .Build();
                });
                
                // ✅ NAPRAWKA OBO: Rejestracja EmbeddedOboTokenManager
                services.AddScoped<EmbeddedOboTokenManager>();
                _logger.LogInformation("[EMBEDDED-API] ✅ Zarejestrowano EmbeddedOboTokenManager dla przepływu OBO");
                
                services.AddScoped<ITokenManager>(provider =>
                {
                    var confidentialClientApp = provider.GetRequiredService<IConfidentialClientApplication>();
                    var memoryCache = provider.GetRequiredService<IMemoryCache>();
                    var logger = provider.GetRequiredService<ILogger<TokenManager>>();
                    var graphConfig = provider.GetRequiredService<GraphApiConfiguration>();
                    
                    return new TokenManager(confidentialClientApp, memoryCache, logger, null, graphConfig);
                });
            }
            else
            {
                _logger.LogWarning("[EMBEDDED-API] ❌ NIE rejestruję IConfidentialClientApplication - warunek nie spełniony!");
                _logger.LogWarning("[EMBEDDED-API] ❌ Aplikacja będzie działać bez uwierzytelniania Graph API!");
            }
            
            // ===== GRAPH API CONFIGURATION =====
            services.AddSingleton<GraphApiConfiguration>();
            _logger.LogInformation("[EMBEDDED-API] ✅ Zarejestrowano GraphApiConfiguration");
            
            // ===== MODERN HTTP SERVICE =====
            // Rejestracja ModernHttpService tylko jeśli IConfidentialClientApplication jest dostępny
            if (_azureAdConfig?.Api?.IsValid() == true)
            {
                services.AddScoped<IModernHttpService>(provider =>
                {
                    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                    var logger = provider.GetRequiredService<ILogger<ModernHttpService>>();
                    var confidentialClientApp = provider.GetRequiredService<IConfidentialClientApplication>();
                    var graphConfig = provider.GetRequiredService<GraphApiConfiguration>();
                    
                    // Tworzymy HttpClient przez factory - to zapobiega ObjectDisposedException
                    var httpClient = httpClientFactory.CreateClient("MicrosoftGraph");
                    return new ModernHttpService(httpClient, logger, confidentialClientApp, graphConfig);
                });
                _logger.LogInformation("[EMBEDDED-API] ✅ Zarejestrowano ModernHttpService z factory pattern");
            }
            else
            {
                _logger.LogWarning("[EMBEDDED-API] ❌ NIE rejestruję ModernHttpService - brak konfiguracji Azure AD");
            }
            
            // ===== GRAPH API SERVICES =====
            services.AddGraphServices(includeAdminNotificationService: false); // Bez admin notifications w embedded
            
            // ===== DOMAIN SERVICES =====
            services.AddScoped<ITeamService, TeamService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IDepartmentService, DepartmentService>();
            services.AddScoped<ISchoolTypeService, SchoolTypeService>();
            services.AddScoped<ISchoolYearService, SchoolYearService>();
            services.AddScoped<ISubjectService, SubjectService>();
            services.AddScoped<ITeamTemplateService, TeamTemplateService>();
            services.AddScoped<IOperationHistoryService, OperationHistoryService>();
            services.AddScoped<IApplicationSettingService, TeamsManager.Core.Services.ApplicationSettingService>();
            services.AddScoped<IChannelService, ChannelService>();
            
            // ===== ORKIESTRATORY =====
            services.AddScoped<ISchoolYearProcessOrchestrator, SchoolYearProcessOrchestrator>();
            services.AddScoped<IDataImportOrchestrator, DataImportOrchestrator>();
            services.AddScoped<ITeamLifecycleOrchestrator, TeamLifecycleOrchestrator>();
            services.AddScoped<IBulkUserManagementOrchestrator, BulkUserManagementOrchestrator>();
            services.AddScoped<IHealthMonitoringOrchestrator, HealthMonitoringOrchestrator>();
            services.AddScoped<IReportingOrchestrator, ReportingOrchestrator>();
            
            // ===== CORS =====
            services.AddCors(options =>
            {
                options.AddPolicy("EmbeddedApiCors", policy =>
                {
                    policy.WithOrigins("https://localhost:3000", "https://localhost:3001", "https://localhost:7037")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });
            
            _logger.LogInformation("[EMBEDDED-API] ✅ Skonfigurowano pełną konfigurację serwisów API dla embedded mode");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMBEDDED-API] ❌ Błąd podczas konfiguracji serwisów: {Message}", ex.Message);
                _logger.LogError("[EMBEDDED-API] ❌ StackTrace: {StackTrace}", ex.StackTrace);
                throw;
            }
        }
        
        private void ConfigureApiPipeline(IApplicationBuilder app)
        {
            // ✅ DODAJ SZCZEGÓŁOWE LOGOWANIE HTTP REQUESTS (podobnie jak w external API)
            app.Use(async (context, next) =>
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<EmbeddedApiServer>>();
                var startTime = DateTime.UtcNow;
                
                // Loguj szczegóły requestu
                logger.LogInformation("[EMBEDDED-API] ==================== NOWY REQUEST ====================");
                logger.LogInformation("[EMBEDDED-API] Method: {Method}", context.Request.Method);
                logger.LogInformation("[EMBEDDED-API] Path: {Path}", context.Request.Path);
                logger.LogInformation("[EMBEDDED-API] Host: {Host}", context.Request.Host);
                logger.LogInformation("[EMBEDDED-API] User-Agent: {UserAgent}", context.Request.Headers.UserAgent.ToString());
                logger.LogInformation("[EMBEDDED-API] Authorization: {Authorization}", 
                    context.Request.Headers.Authorization.ToString().Length > 0 ? "Bearer [REDACTED]" : "BRAK");
                
                await next.Invoke();
                
                // Loguj szczegóły response
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                logger.LogInformation("[EMBEDDED-API] Response: {StatusCode} w {Duration:F1}ms ({StatusText})", 
                    context.Response.StatusCode, duration, 
                    ((System.Net.HttpStatusCode)context.Response.StatusCode).ToString());
                logger.LogInformation("[EMBEDDED-API] ==================== KONIEC REQUEST ====================");
            });
            
            // ✅ NAPRAWKA OBO: TokenValidationMiddleware musi być przed routing
            app.UseMiddleware<TokenValidationMiddleware>();
            
            app.UseRouting();
            
            // ✅ CORS (musi być przed Authentication)
            app.UseCors("EmbeddedApiCors");
            
            // ✅ Mapowanie kontrolerów
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            
            _logger.LogInformation("[EMBEDDED-API] ✅ Skonfigurowano pipeline API z TokenValidationMiddleware");
        }

        private int FindAvailablePort(int startPort, int endPort)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var usedPorts = ipGlobalProperties.GetActiveTcpListeners();

            for (int port = startPort; port <= endPort; port++)
            {
                bool isPortUsed = false;
                foreach (var endpoint in usedPorts)
                {
                    if (endpoint.Port == port)
                    {
                        isPortUsed = true;
                        break;
                    }
                }

                if (!isPortUsed)
                {
                    _logger.LogDebug("🔍 Port {Port} jest dostępny", port);
                    return port;
                }
            }

            throw new InvalidOperationException($"Nie znaleziono dostępnego portu w zakresie {startPort}-{endPort}");
        }

        private async Task EnsureDevelopmentCertificateAsync()
        {
            try
            {
                _logger.LogInformation("🔐 Sprawdzanie certyfikatu SSL...");

                // Sprawdź czy certyfikat deweloperski istnieje
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "dev-certs https --check --trust",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogInformation("🔧 Generowanie nowego certyfikatu SSL...");
                    
                    // Wygeneruj nowy certyfikat
                    var generateProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = "dev-certs https --trust",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    generateProcess.Start();
                    await generateProcess.WaitForExitAsync();

                    if (generateProcess.ExitCode == 0)
                    {
                        _logger.LogInformation("✅ Certyfikat SSL wygenerowany i zaufany");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Nie udało się wygenerować certyfikatu SSL - używam HTTP");
                    }
                }
                else
                {
                    _logger.LogInformation("✅ Certyfikat SSL jest już skonfigurowany");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Błąd podczas konfiguracji certyfikatu SSL - używam HTTP");
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _host?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
} 