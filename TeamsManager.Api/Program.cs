// Plik: TeamsManager.Api/Program.cs
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.IO;        // Dla Path i File
using System.Text.Json; // Dla JsonSerializer
using TeamsManager.Api.Configuration;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using TeamsManager.Core.Services.UserContext;
using TeamsManager.Data;
using TeamsManager.Data.Repositories;
using System;
using TeamsManager.Core.Enums;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.OpenApi.Models;
using TeamsManager.Api.Swagger;
using TeamsManager.Core.Extensions;
using TeamsManager.Api.Hubs; // <-- Dodane dla NotificationHub
using TeamsManager.Core.Services.PowerShellServices; // Dla StubNotificationService (jeśli tam jest) lub odpowiedniej przestrzeni nazw

var builder = WebApplication.CreateBuilder(args);

// Wczytaj konfigurację OAuth na samym początku
var oauthApiConfig = ApiAuthConfig.LoadApiOAuthConfig(builder.Configuration);

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

// Rejestracja Repozytoriów
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

// ========== WCZEŚNIEJ DODANA REJESTRACJA - StubNotificationService ==========
builder.Services.AddScoped<INotificationService, StubNotificationService>();
// ==========================================================================

// Rejestracja Serwisów Aplikacyjnych
builder.Services.AddPowerShellServices(); // To rejestruje IPowerShellConnectionService, IPowerShellCacheService, IPowerShellTeamManagementService, IPowerShellUserManagementService, IPowerShellBulkOperationsService, IPowerShellService
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
// ========== NOWA LINIA - Mapowanie Huba SignalR ==========
app.MapHub<NotificationHub>("/notificationHub");
// =========================================================

// ----- KONIEC SEKCJI KONFIGURACJI HTTP PIPELINE -----

app.Run();