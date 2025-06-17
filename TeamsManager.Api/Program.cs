// Plik: TeamsManager.Api/Program.cs
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.IO;        // Dla Path i File
using System.Text.Json; // Dla JsonSerializer
using TeamsManager.Api.Configuration;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using TeamsManager.Core.Services.Auth;
using TeamsManager.Core.Services.UserContext;
using TeamsManager.Data;
using TeamsManager.Data.Repositories;
using TeamsManager.Data.UnitOfWork;
using System;
using TeamsManager.Core.Enums;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.OpenApi.Models;
using TeamsManager.Api.Swagger;
using TeamsManager.Core.Extensions;
using TeamsManager.Api.Hubs; // <-- Dodane dla NotificationHub
using TeamsManager.Api.Services; // <-- Dodane dla SignalRNotificationService
using TeamsManager.Api.HealthChecks; // <-- Dodane dla DependencyInjectionHealthCheck
using TeamsManager.Core.Abstractions.Services.Synchronization; // <-- Dodane dla synchronizatorów (Etap 4/8)
using TeamsManager.Core.Abstractions.Services.Cache; // <-- Dodane dla CacheInvalidationService (Etap 7/8)
using TeamsManager.Core.Services.Synchronization; // <-- Dodane dla synchronizatorów (Etap 4/8)
using TeamsManager.Core.Services.Cache; // <-- Dodane dla implementacji Cache (Etap 7/8)
using TeamsManager.Core.Models.Graph; // <-- Dodane dla GraphTeam, GraphUser, GraphChannel
using Microsoft.Extensions.Http.Resilience;
using Polly;
using TeamsManager.Core.Common;
using TeamsManager.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Wczytaj konfigurację OAuth na samym początku
// W środowisku Test pomijamy walidację konfiguracji OAuth
var skipValidation = builder.Environment.EnvironmentName == "Test";
var oauthApiConfig = ApiAuthConfig.LoadApiOAuthConfig(builder.Configuration, skipValidation);

if (string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.TenantId) ||
    string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.ClientId) ||
    string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.ClientSecret) ||
    string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.Audience))
{
    var errorMessage = "[KRYTYCZNY BŁĄD KONFIGURACJI API w Program.cs] Kluczowe wartości AzureAd (TenantId, ClientId, ClientSecret, Audience) " +
                       "nie zostały w pełni skonfigurowane. Uwierzytelnianie JWT i/lub przepływ On-Behalf-Of mogą nie działać poprawnie. " +
                       "Sprawdź appsettings.json, User Secrets lub inne źródła konfiguracji.";
    Console.Error.WriteLine(errorMessage);
}

// ----- POCZĘTEK SEKCJI REJESTRACJI SERWISÓW -----
builder.Services.AddControllers();

// ========== DODANIE HEALTH CHECKS ==========
builder.Services.AddHealthChecks()
    .AddCheck<DependencyInjectionHealthCheck>("di_check")
    .AddCheck<GraphConnectionHealthCheck>("graph_connection", 
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "graph", "external" });
// ==========================================

// ========== NOWA LINIA - Dodanie usług SignalR ==========
builder.Services.AddSignalR();
// ========================================================

builder.Services.AddApiVersioning(config =>
{
    config.DefaultApiVersion = new ApiVersion(1, 0);
    config.AssumeDefaultVersionWhenUnspecified = true;
    config.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("version"),
        new HeaderApiVersionReader("X-Version"),
        new UrlSegmentApiVersionReader()
    );
    config.ApiVersionSelector = new DefaultApiVersionSelector(config);
}).AddApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TeamsManager API",
        Version = "v1.0",
        Description = @"
## 📋 Opis API
API dla aplikacji TeamsManager - kompleksowe zarządzanie zespołami Microsoft Teams w środowisku edukacyjnym.
(...)
        ", // Skrócono dla zwięzłości
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "TeamsManager Support",
            Email = "support@teamsmanager.local",
            Url = new Uri("https://github.com/teamsmanager/api")
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    options.SwaggerDoc("v2", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TeamsManager API",
        Version = "v2.0",
        Description = @"
## 🚀 TeamsManager API v2.0 (Przyszła wersja)
(...)
        ", // Skrócono dla zwięzłości
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "TeamsManager Support",
            Email = "support@teamsmanager.local",
            Url = new Uri("https://github.com/teamsmanager/api")
        }
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = @"
## 🔐 Uwierzytelnianie JWT Bearer Token
(...)
        " // Skrócono dla zwięzłości
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
        Console.WriteLine($"✅ Swagger: Załadowano komentarze XML z: {xmlPath}");
    }
    else
    {
        Console.WriteLine($"⚠️ Swagger: Nie znaleziono pliku XML: {xmlPath}");
    }

    var coreXmlFile = "TeamsManager.Core.xml";
    var coreXmlPath = Path.Combine(AppContext.BaseDirectory, coreXmlFile);
    if (File.Exists(coreXmlPath))
    {
        options.IncludeXmlComments(coreXmlPath);
        Console.WriteLine($"✅ Swagger: Załadowano komentarze XML z Core: {coreXmlPath}");
    }

    options.SchemaFilter<ExampleSchemaFilter>();
    options.OperationFilter<AuthorizationOperationFilter>();
    options.DocumentFilter<TagsDocumentFilter>();

    options.SchemaGeneratorOptions.SchemaIdSelector = type => type.FullName?.Replace("+", ".");
    options.MapType<UserRole>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "string",
        Enum = System.Enum.GetNames<UserRole>().Select(name => new Microsoft.OpenApi.Any.OpenApiString(name)).ToArray<Microsoft.OpenApi.Any.IOpenApiAny>()
    });
    options.MapType<TeamStatus>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "string",
        Enum = System.Enum.GetNames<TeamStatus>().Select(name => new Microsoft.OpenApi.Any.OpenApiString(name)).ToArray<Microsoft.OpenApi.Any.IOpenApiAny>()
    });
    options.MapType<TeamsManager.Core.Enums.OperationType>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "string",
        Enum = System.Enum.GetNames<TeamsManager.Core.Enums.OperationType>().Select(name => new Microsoft.OpenApi.Any.OpenApiString(name)).ToArray<Microsoft.OpenApi.Any.IOpenApiAny>()
    });
});
builder.Services.AddDbContext<TeamsManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddMemoryCache();

// Rejestracja Unit of Work
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

// Rejestracja Repozytoriów - ZACHOWUJEMY dla kompatybilności wstecznej!
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<ITeamTemplateRepository, TeamTemplateRepository>();
builder.Services.AddScoped<ISchoolYearRepository, SchoolYearRepository>();
builder.Services.AddScoped<IOperationHistoryRepository, OperationHistoryRepository>();
builder.Services.AddScoped<IApplicationSettingRepository, ApplicationSettingRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IGenericRepository<SchoolType>, GenericRepository<SchoolType>>();
builder.Services.AddScoped<IGenericRepository<Department>, GenericRepository<Department>>();
builder.Services.AddScoped<IGenericRepository<UserSchoolType>, GenericRepository<UserSchoolType>>();
builder.Services.AddScoped<IGenericRepository<UserSubject>, GenericRepository<UserSubject>>();

// ========== FINALIZACJA SIGNALR - Zmiana na SignalRNotificationService ==========
// Wzorzec: Conditional Registration - sprawdź IsProd dla finalnej rejestracji
if (builder.Environment.IsProduction() || builder.Configuration.GetValue<bool>("SignalR:Enabled", true))
{
    builder.Services.AddScoped<INotificationService, SignalRNotificationService>();
}
else
{
    builder.Services.AddScoped<INotificationService, StubNotificationService>();
}
// ================================================================================

// ========== REJESTRACJA ADMIN NOTIFICATION SERVICE (ETAP 5/7) ==========
// Rejestracja Admin Notification Service
if (builder.Environment.IsDevelopment() || !builder.Configuration.GetValue<bool>("AdminNotifications:Enabled", false))
{
    builder.Services.AddScoped<IAdminNotificationService, StubAdminNotificationService>();
}
else
{
    builder.Services.AddScoped<IAdminNotificationService, GraphAdminNotificationService>();
}
// ====================================================================

// ========== NOWA REJESTRACJA - Synchronizatory Graph-DB (Etap 4/8) ==========
builder.Services.AddScoped<IGraphSynchronizer<Team, GraphTeam>, TeamSynchronizer>();
builder.Services.AddScoped<IGraphSynchronizer<User, GraphUser>, UserSynchronizer>();
builder.Services.AddScoped<IGraphSynchronizer<Channel, GraphChannel>, ChannelSynchronizer>();

// ETAP 7/8: Centralizacja inwalidacji cache
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
// W przyszłości dodaj więcej synchronizatorów:
// builder.Services.AddScoped<IGraphSynchronizer<User>, UserSynchronizer>();
// ===========================================================================

// Rejestracja Serwisów Aplikacyjnych - Graph API Services
builder.Services.AddGraphServices(includeAdminNotificationService: true); // Rejestruje wszystkie Graph API services

builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<ISchoolTypeService, SchoolTypeService>();
builder.Services.AddScoped<ISchoolYearService, SchoolYearService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<ITeamTemplateService, TeamTemplateService>();
builder.Services.AddScoped<IOperationHistoryService, OperationHistoryService>();
builder.Services.AddScoped<IApplicationSettingService, ApplicationSettingService>();
builder.Services.AddScoped<IChannelService, ChannelService>();

// ========== NOWA REJESTRACJA - Orkiestrator Procesów Szkolnych ==========
builder.Services.AddScoped<ISchoolYearProcessOrchestrator, SchoolYearProcessOrchestrator>();
// ========================================================================

// ========== NOWA REJESTRACJA - Orkiestrator Importu Danych ==========
builder.Services.AddScoped<IDataImportOrchestrator, DataImportOrchestrator>();

// Rejestracja TeamLifecycleOrchestrator (orkiestrator cyklu życia zespołów)
builder.Services.AddScoped<ITeamLifecycleOrchestrator, TeamLifecycleOrchestrator>();

// ========== NOWA REJESTRACJA - Orkiestrator Zarządzania Użytkownikami ==========
builder.Services.AddScoped<IBulkUserManagementOrchestrator, BulkUserManagementOrchestrator>();
// ================================================================================

// ========== NOWA REJESTRACJA - Orkiestrator Monitorowania Zdrowia ==========
builder.Services.AddScoped<IHealthMonitoringOrchestrator, HealthMonitoringOrchestrator>();
// ============================================================================

// ========== NOWA REJESTRACJA - Orkiestrator Raportowania ==========
builder.Services.AddScoped<IReportingOrchestrator, ReportingOrchestrator>();
// ===================================================================

// ========== NOWA OPTYMALIZACJA: HTTP RESILIENCE ==========
// Konfiguracja Modern HTTP Resilience z Microsoft.Extensions.Http.Resilience
builder.Services.AddHttpClient("MicrosoftGraph", client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "TeamsManager/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    // Retry Policy
    options.Retry.ShouldHandle = args => args.Outcome switch
    {
        { } outcome when HttpClientResiliencePredicates.IsTransient(outcome) => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.RequestTimeout => PredicateResult.True(),
        _ => PredicateResult.False()
    };
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.UseJitter = true;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    
    // Circuit Breaker  
    options.CircuitBreaker.ShouldHandle = args => args.Outcome switch
    {
        { } outcome when HttpClientResiliencePredicates.IsTransient(outcome) => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.InternalServerError => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.BadGateway => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.GatewayTimeout => PredicateResult.True(),
        _ => PredicateResult.False()
    };
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(60);

    // Timeout
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
    
    // Rate Limiter dla Graph API - pozostawiono bez dodatkowego rate limitera
    // RateLimiter w Polly może być skonfigurowany jeśli potrzebny
});

// Dodatkowy HttpClient dla zewnętrznych API
builder.Services.AddHttpClient("ExternalApis")
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.CircuitBreaker.FailureRatio = 0.7;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
});
// =========================================================

// ========== REJESTRACJA NOWOCZESNYCH SERWISÓW ==========
// Modern HTTP Service zastępujący stare wzorce resilience
builder.Services.AddScoped<IModernHttpService, ModernHttpService>();

// Modern Circuit Breaker kompatybilny z HTTP Resilience
builder.Services.AddSingleton<ModernCircuitBreaker>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<ModernCircuitBreaker>>();
    return new ModernCircuitBreaker(
        failureThreshold: 5,
        openDuration: TimeSpan.FromMinutes(1),
        logger: logger
    );
});

// IConfidentialClientApplication dla TokenManager
builder.Services.AddScoped<IConfidentialClientApplication>(provider =>
{
    var authority = $"{oauthApiConfig.AzureAd.Instance?.TrimEnd('/')}/{oauthApiConfig.AzureAd.TenantId}";
    var logger = provider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Konfiguracja IConfidentialClientApplication: ClientId='{ApiAppClientId}', Authority='{Authority}', ClientSecret is set: {IsSecretSet}",
        oauthApiConfig.AzureAd.ClientId,
        authority,
        !string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.ClientSecret));
     
    return ConfidentialClientApplicationBuilder.Create(oauthApiConfig.AzureAd.ClientId)
        .WithClientSecret(oauthApiConfig.AzureAd.ClientSecret)
        .WithAuthority(new Uri(authority))
        .Build();
});

// Enhanced Token Manager - używa GraphApiConfiguration
builder.Services.AddScoped<ITokenManager>(provider =>
{
    var confidentialClientApp = provider.GetRequiredService<IConfidentialClientApplication>();
    var memoryCache = provider.GetRequiredService<IMemoryCache>();
    var logger = provider.GetRequiredService<ILogger<TokenManager>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    var graphConfig = provider.GetRequiredService<GraphApiConfiguration>();
    
    return new TokenManager(confidentialClientApp, memoryCache, logger, configuration, graphConfig);
});

const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options => {
    options.AddPolicy(name: MyAllowSpecificOrigins, policy => {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin() // ZMIEŃ NA PRODUKCJI!
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{oauthApiConfig.AzureAd.Instance?.TrimEnd('/')}/{oauthApiConfig.AzureAd.TenantId}/v2.0";
        options.Audience = oauthApiConfig.AzureAd.Audience;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                $"{oauthApiConfig.AzureAd.Instance?.TrimEnd('/')}/{oauthApiConfig.AzureAd.TenantId}/v2.0",
                $"https://sts.windows.net/{oauthApiConfig.AzureAd.TenantId}/"
            },
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
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
                    Console.WriteLine($"[API Auth] SignalR JWT token extracted from query string for path: {path}");
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context => {
                Console.WriteLine($"[API Auth] BŁĄD autentykacji: {context.Exception.Message}");
                System.Diagnostics.Debug.WriteLine($"[API Auth] BŁĄD autentykacji: {context.Exception.ToString()}");
                if (context.Exception is SecurityTokenInvalidAudienceException)
                {
                    Console.WriteLine($"[API Auth] Błędny Audience. Oczekiwano: {options.Audience}, Otrzymano w tokenie: {(context.Exception as SecurityTokenInvalidAudienceException)?.InvalidAudience}");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context => {
                Console.WriteLine($"[API Auth] Token pomyślnie zwalidowany dla użytkownika: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            },
            OnChallenge = context => {
                Console.WriteLine($"[API Auth] JWT Challenge: Błąd='{context.Error}', Opis='{context.ErrorDescription}'");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // ... ewentualne polityki ...
});
// ----- KONIEC SEKCJI REJESTRACJI SERWISÓW -----

var app = builder.Build();

// ========== DODANIE WERYFIKACJI DI PODCZAS STARTU ==========
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("=== Weryfikacja konfiguracji DI ===");
    
    // Sprawdź krytyczne serwisy
    var criticalServices = new[]
    {
        ("IOperationHistoryService", typeof(IOperationHistoryService)),
        ("INotificationService", typeof(INotificationService)),
        ("ICurrentUserService", typeof(ICurrentUserService)),
        ("ITeamService", typeof(ITeamService)),
        ("IUserService", typeof(IUserService)),
        ("IDepartmentService", typeof(IDepartmentService)),
        ("IChannelService", typeof(IChannelService)),
        ("ISubjectService", typeof(ISubjectService)),
        ("IApplicationSettingService", typeof(IApplicationSettingService)),
        ("ISchoolTypeService", typeof(ISchoolTypeService)),
        ("ISchoolYearService", typeof(ISchoolYearService)),
        ("ITeamTemplateService", typeof(ITeamTemplateService)),
        ("ITokenManager", typeof(ITokenManager)),
        ("IBulkUserManagementOrchestrator", typeof(IBulkUserManagementOrchestrator))
    };

    var allServicesOk = true;
    
    foreach (var (name, type) in criticalServices)
    {
        try
        {
            var service = scope.ServiceProvider.GetService(type);
            if (service != null)
            {
                logger.LogInformation($"✅ {name} - OK");
            }
            else
            {
                logger.LogError($"❌ {name} - NOT REGISTERED");
                allServicesOk = false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"❌ {name} - ERROR");
            allServicesOk = false;
        }
    }
    
    if (!allServicesOk)
    {
        logger.LogError("KRYTYCZNY BŁĄD: Nie wszystkie wymagane serwisy są zarejestrowane!");
    }
    else
    {
        logger.LogInformation("✅ Wszystkie krytyczne serwisy są poprawnie zarejestrowane");
    }
    
    logger.LogInformation("=== Koniec weryfikacji DI ===");
}
// ============================================================

// ----- POCZĘTEK SEKCJI KONFIGURACJI HTTP PIPELINE -----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TeamsManager API v1.0");
        options.SwaggerEndpoint("/swagger/v2/swagger.json", "TeamsManager API v2.0");
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        options.DefaultModelExpandDepth(2);
        options.DefaultModelsExpandDepth(-1);
        options.EnableDeepLinking();
        options.EnableFilter();
        options.ShowExtensions();
    });
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ========== MAPOWANIE HEALTH CHECKS ==========
app.MapHealthChecks("/health");

// Dodatkowy endpoint ze szczegółowymi informacjami
app.MapHealthChecks("/health/detailed", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                data = e.Value.Data,
                exception = e.Value.Exception?.Message
            })
        };
        
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(response, 
                new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                }));
    }
});
// =============================================

// ========== NOWA LINIA - Mapowanie Huba SignalR ==========
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<MonitoringHub>("/monitoringHub");
// =========================================================

// ----- KONIEC SEKCJI KONFIGURACJI HTTP PIPELINE -----

app.Run();

// Dodane dla testów integracyjnych
public partial class Program { }