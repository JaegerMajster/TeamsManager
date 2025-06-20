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

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Embedded API Server który uruchamia się w tle aplikacji WPF
    /// Automatycznie dobiera porty, generuje certyfikaty SSL i zarządza cyklem życia
    /// </summary>
    public class EmbeddedApiServer : IDisposable
    {
        private readonly ILogger<EmbeddedApiServer> _logger;
        private IHost? _host;
        private int _httpsPort;
        private int _httpPort;
        private bool _isRunning;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public int HttpsPort => _httpsPort;
        public int HttpPort => _httpPort;
        public bool IsRunning => _isRunning;
        public string BaseUrl => $"https://localhost:{_httpsPort}";

        public EmbeddedApiServer(ILogger<EmbeddedApiServer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Uruchamia embedded API server
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                _logger.LogInformation("🚀 Uruchamianie Embedded API Server...");

                // 1. Znajdź dostępne porty
                _httpsPort = FindAvailablePort(7037, 7100);
                _httpPort = FindAvailablePort(5182, 5200);

                _logger.LogInformation("📡 Wybrane porty: HTTPS={HttpsPort}, HTTP={HttpPort}", _httpsPort, _httpPort);

                // 2. Przygotuj certyfikat SSL
                await EnsureDevelopmentCertificateAsync();

                // 3. Skonfiguruj i uruchom host
                _host = CreateHostBuilder().Build();
                
                await _host.StartAsync(_cancellationTokenSource.Token);
                _isRunning = true;

                _logger.LogInformation("✅ Embedded API Server uruchomiony pomyślnie na {BaseUrl}", BaseUrl);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Błąd podczas uruchamiania Embedded API Server");
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
                    logging.SetMinimumLevel(LogLevel.Warning); // Tylko błędy i ostrzeżenia
                });
        }

        private void ConfigureApiServices(IServiceCollection services)
        {
            // Dodaj podstawowe serwisy ASP.NET Core
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            
            // Dodaj CORS dla lokalnej komunikacji
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins("https://localhost", "http://localhost")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            // Health checks
            services.AddHealthChecks();

            // TODO: Dodać wszystkie serwisy z TeamsManager.Api
            // services.AddScoped<IGraphConnectionService, GraphConnectionService>();
            // itd...
        }

        private void ConfigureApiPipeline(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseCors();
            
            // Health check endpoint
            app.UseHealthChecks("/health");
            
            // Swagger tylko w development
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "TeamsManager API v1");
                c.RoutePrefix = "swagger";
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
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