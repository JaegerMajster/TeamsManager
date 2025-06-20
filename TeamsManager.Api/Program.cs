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
using System.IO;
using System.Text.Json;
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
using TeamsManager.Api.Hubs;
using TeamsManager.Api.Services;
using TeamsManager.Api.HealthChecks;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Services.Synchronization;
using TeamsManager.Core.Services.Cache;
using TeamsManager.Core.Models.Graph;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using TeamsManager.Core.Common;
using TeamsManager.Application.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamsManager.Api.Services.Configuration;
using TeamsManager.Api.Models.Configuration;
using TeamsManager.Api.HealthChecks;
using TeamsManager.Api.Hubs;
using TeamsManager.Api.Services;
using TeamsManager.Api.Swagger;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;

var builder = WebApplication.CreateBuilder(args);

// ===== NOWY UNIWERSALNY SYSTEM KONFIGURACJI (Clean Architecture) =====
// Rejestracja systemu konfiguracji zgodnego z zasadami DRY
builder.Services.AddSingleton<TeamsManager.Api.Services.Configuration.AdvancedEncryptionService>();
builder.Services.AddSingleton<TeamsManager.Core.Abstractions.Services.IConfigurationService, 
    TeamsManager.Api.Services.Configuration.ConfigurationService>();

// Ładowanie konfiguracji Azure AD z nowego systemu
var configService = builder.Services.BuildServiceProvider()
    .GetRequiredService<TeamsManager.Core.Abstractions.Services.IConfigurationService>();

var azureAdConfig = await configService.GetConfigurationAsync<TeamsManager.Core.Models.Configuration.AzureAdConfiguration>("azure-ad");

if (azureAdConfig?.Api?.IsValid() != true)
{
    Console.WriteLine("[BŁĄD] Brak konfiguracji Azure AD w systemie V2.0!");
    Console.WriteLine("Uruchom najpierw aplikację UI, aby skonfigurować Azure AD.");
    throw new InvalidOperationException("Brak konfiguracji Azure AD. Uruchom UI aby skonfigurować system.");
}

builder.Services.AddSingleton(azureAdConfig);
Console.WriteLine("[SUKCES] Załadowano konfigurację Azure AD z systemu V2.0");

builder.Services.AddControllers();

builder.Services.AddHealthChecks()
    .AddCheck<DependencyInjectionHealthCheck>("di_check")
    .AddCheck<GraphConnectionHealthCheck>("graph_connection", 
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "graph", "external" });

builder.Services.AddSignalR();

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
        ",
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
        ",
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
        "
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
// Ładowanie konfiguracji aplikacji z nowego systemu
var applicationConfig = await configService.GetConfigurationAsync<TeamsManager.Core.Models.Configuration.ApplicationConfiguration>("application");
if (applicationConfig?.IsValid() != true)
{
    Console.WriteLine("[OSTRZEŻENIE] Brak konfiguracji aplikacji - tworzę domyślną");
    applicationConfig = builder.Environment.IsDevelopment() ? 
        TeamsManager.Api.Services.Configuration.DefaultApplicationConfig.CreateDevelopment() :
        TeamsManager.Api.Services.Configuration.DefaultApplicationConfig.CreateDefault();
    await configService.SaveConfigurationAsync("application", applicationConfig);
    Console.WriteLine("[SUKCES] Utworzono domyślną konfigurację aplikacji");
}

builder.Services.AddSingleton(applicationConfig);

builder.Services.AddDbContext<TeamsManagerDbContext>(options =>
    options.UseSqlite(applicationConfig.ConnectionStrings.DefaultConnection));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddMemoryCache();

// Rejestracja Unit of Work
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

// Rejestracja repozytoriów
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

// Konfiguracja SignalR Notification Service
if (builder.Environment.IsProduction() || builder.Configuration.GetValue<bool>("SignalR:Enabled", true))
{
    builder.Services.AddScoped<INotificationService, SignalRNotificationService>();
}
else
{
    builder.Services.AddScoped<INotificationService, StubNotificationService>();
}

// Konfiguracja Admin Notification Service z nowego systemu
if (builder.Environment.IsDevelopment() || !applicationConfig.AdminNotifications.Enabled)
{
    builder.Services.AddScoped<IAdminNotificationService, StubAdminNotificationService>();
}
else
{
    builder.Services.AddScoped<IAdminNotificationService, GraphAdminNotificationService>();
}

// Rejestracja synchronizatorów Graph-DB
builder.Services.AddScoped<IGraphSynchronizer<Team, GraphTeam>, TeamSynchronizer>();
builder.Services.AddScoped<IGraphSynchronizer<User, GraphUser>, UserSynchronizer>();
builder.Services.AddScoped<IGraphSynchronizer<Channel, GraphChannel>, ChannelSynchronizer>();

// Centralizacja inwalidacji cache
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

// Rejestracja Graph API Services
builder.Services.AddGraphServices(includeAdminNotificationService: true);

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

// Rejestracja orkiestratorów
builder.Services.AddScoped<ISchoolYearProcessOrchestrator, SchoolYearProcessOrchestrator>();
builder.Services.AddScoped<IDataImportOrchestrator, DataImportOrchestrator>();
builder.Services.AddScoped<ITeamLifecycleOrchestrator, TeamLifecycleOrchestrator>();
builder.Services.AddScoped<IBulkUserManagementOrchestrator, BulkUserManagementOrchestrator>();
builder.Services.AddScoped<IHealthMonitoringOrchestrator, HealthMonitoringOrchestrator>();
builder.Services.AddScoped<IReportingOrchestrator, ReportingOrchestrator>();

// Konfiguracja HTTP Resilience dla Microsoft Graph z nowego systemu
var graphSettings = applicationConfig.ModernHttpResilience.MicrosoftGraph;
builder.Services.AddHttpClient("MicrosoftGraph", client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "TeamsManager/1.0");
    client.Timeout = TimeSpan.FromSeconds(graphSettings.Timeout.TotalRequestTimeoutSeconds);
})
.AddStandardResilienceHandler(options =>
{
    // Polityka ponawiania z konfiguracji
    options.Retry.ShouldHandle = args => args.Outcome switch
    {
        { } outcome when HttpClientResiliencePredicates.IsTransient(outcome) => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.RequestTimeout => PredicateResult.True(),
        _ => PredicateResult.False()
    };
    options.Retry.MaxRetryAttempts = graphSettings.Retry.MaxAttempts;
    options.Retry.UseJitter = graphSettings.Retry.UseJitter;
    options.Retry.BackoffType = graphSettings.Retry.BackoffType == "Exponential" ? 
        Polly.DelayBackoffType.Exponential : Polly.DelayBackoffType.Linear;
    options.Retry.Delay = TimeSpan.FromSeconds(graphSettings.Retry.BaseDelaySeconds);
    
    // Circuit Breaker z konfiguracji
    options.CircuitBreaker.ShouldHandle = args => args.Outcome switch
    {
        { } outcome when HttpClientResiliencePredicates.IsTransient(outcome) => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.InternalServerError => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.BadGateway => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable => PredicateResult.True(),
        { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.GatewayTimeout => PredicateResult.True(),
        _ => PredicateResult.False()
    };
    options.CircuitBreaker.FailureRatio = graphSettings.CircuitBreaker.FailureRatio;
    options.CircuitBreaker.MinimumThroughput = graphSettings.CircuitBreaker.MinimumThroughput;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(graphSettings.CircuitBreaker.SamplingDurationSeconds);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(graphSettings.CircuitBreaker.BreakDurationSeconds);

    // Timeout z konfiguracji
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(graphSettings.Timeout.TotalRequestTimeoutSeconds);
});

// HttpClient dla zewnętrznych API z konfiguracji
var externalSettings = applicationConfig.ModernHttpResilience.ExternalApis;
builder.Services.AddHttpClient("ExternalApis")
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = externalSettings.Retry.MaxAttempts;
    options.Retry.Delay = TimeSpan.FromSeconds(externalSettings.Retry.BaseDelaySeconds);
    options.CircuitBreaker.FailureRatio = externalSettings.CircuitBreaker.FailureRatio;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(externalSettings.CircuitBreaker.BreakDurationSeconds);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(externalSettings.Timeout.TotalRequestTimeoutSeconds);
});

// Rejestracja nowoczesnych serwisów HTTP
builder.Services.AddScoped<IModernHttpService, ModernHttpService>();

// Modern Circuit Breaker
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
    var authority = $"{azureAdConfig.Instance?.TrimEnd('/')}/{azureAdConfig.TenantId}";
    var logger = provider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Konfiguracja IConfidentialClientApplication: ClientId='{ApiAppClientId}', Authority='{Authority}', ClientSecret is set: {IsSecretSet}",
        azureAdConfig.Api.ClientId,
        authority,
        !string.IsNullOrWhiteSpace(azureAdConfig.Api.ClientSecret));
     
    return ConfidentialClientApplicationBuilder.Create(azureAdConfig.Api.ClientId)
        .WithClientSecret(azureAdConfig.Api.ClientSecret)
        .WithAuthority(new Uri(authority))
        .Build();
});

// Enhanced Token Manager
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
            policy.AllowAnyOrigin() // TODO: Zmienić na produkcji na konkretne domeny
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{azureAdConfig.Instance?.TrimEnd('/')}/{azureAdConfig.TenantId}/v2.0";
        options.Audience = azureAdConfig.Api.Audience;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                $"{azureAdConfig.Instance?.TrimEnd('/')}/{azureAdConfig.TenantId}/v2.0",
                $"https://sts.windows.net/{azureAdConfig.TenantId}/"
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
                    Console.WriteLine($"[API Auth] SignalR JWT token wyodrębniony z query string dla ścieżki: {path}");
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context => {
                Console.WriteLine($"[API Auth] BŁĄD uwierzytelniania: {context.Exception.Message}");
                System.Diagnostics.Debug.WriteLine($"[API Auth] BŁĄD uwierzytelniania: {context.Exception.ToString()}");
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
    // TODO: Dodać polityki autoryzacji jeśli potrzebne
});

var app = builder.Build();

// Weryfikacja konfiguracji Dependency Injection podczas startu
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("=== Weryfikacja konfiguracji DI ===");
    
    // Sprawdzenie krytycznych serwisów
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
                logger.LogError($"❌ {name} - NIE ZAREJESTROWANY");
                allServicesOk = false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"❌ {name} - BŁĄD");
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

// Konfiguracja HTTP Pipeline
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

// Mapowanie Health Checks
app.MapHealthChecks("/health");

// Endpoint ze szczegółowymi informacjami o zdrowiu systemu
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

// Mapowanie hubów SignalR
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<MonitoringHub>("/monitoringHub");

app.Run();

// Klasa dla testów integracyjnych
public partial class Program { }