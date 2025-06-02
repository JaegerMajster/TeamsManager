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
using TeamsManager.Core.Models; // Dla IGenericRepository<SchoolType> itp.
using TeamsManager.Core.Services;
using TeamsManager.Core.Services.UserContext;
using TeamsManager.Data;
using TeamsManager.Data.Repositories;
using System; // Dla Environment
using TeamsManager.Core.Enums; // Dla UserRole, TeamStatus, OperationType
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.OpenApi.Models;
using TeamsManager.Api.Swagger; // Dla filtrów Swagger

// --- POCZĘTEK SEKCJI DEFINICJI POMOCNICZYCH (PRZED TOP-LEVEL STATEMENTS) ---
// --- KONIEC SEKCJI DEFINICJI POMOCNICZYCH ---

var builder = WebApplication.CreateBuilder(args);

// Wczytaj konfigurację OAuth na samym początku
var oauthApiConfig = ApiAuthConfig.LoadApiOAuthConfig(builder.Configuration);

// Podstawowa walidacja krytycznych wartości konfiguracyjnych dla API (Audience, ClientId API, ClientSecret API, TenantId)
// Metoda LoadApiOAuthConfig już loguje błędy, jeśli te wartości są puste.
// Można dodać tutaj rzucanie wyjątku, jeśli chcemy zatrzymać start aplikacji przy braku krytycznej konfiguracji.
if (string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.TenantId) ||
    string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.ClientId) ||    // ClientId aplikacji API (dla OBO)
    string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.ClientSecret) || // ClientSecret aplikacji API (dla OBO)
    string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.Audience))       // Audience, którego API oczekuje od UI
{
    var errorMessage = "[KRYTYCZNY BŁĄD KONFIGURACJI API w Program.cs] Kluczowe wartości AzureAd (TenantId, ClientId, ClientSecret, Audience) " +
                       "nie zostały w pełni skonfigurowane. Uwierzytelnianie JWT i/lub przepływ On-Behalf-Of mogą nie działać poprawnie. " +
                       "Sprawdź appsettings.json, User Secrets lub inne źródła konfiguracji.";
    // Logowanie do konsoli jest już w ApiAuthConfig, można tu dodać logowanie przez ILogger, gdy będzie dostępny,
    // lub po prostu rzucić wyjątek.
    Console.Error.WriteLine(errorMessage);
    // throw new InvalidOperationException(errorMessage); // Rozważ odkomentowanie, aby zatrzymać aplikację
}


// ----- POCZĘTEK SEKCJI REJESTRACJI SERWISÓW -----
builder.Services.AddControllers();

// Konfiguracja API Versioning
builder.Services.AddApiVersioning(config =>
{
    config.DefaultApiVersion = new ApiVersion(1, 0);
    config.AssumeDefaultVersionWhenUnspecified = true;
    
    // Metody obsługi wersji API
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
    // Dodaj dokumenty Swagger dla różnych wersji
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TeamsManager API",
        Version = "v1.0",
        Description = @"
## 📋 Opis API
API dla aplikacji TeamsManager - kompleksowe zarządzanie zespołami Microsoft Teams w środowisku edukacyjnym.

## 🔐 Uwierzytelnianie
API wymaga uwierzytelniania JWT Bearer Token otrzymanego z Azure AD. 
Aby przetestować endpointy:
1. Kliknij przycisk **Authorize** 🔒
2. Wprowadź token w formacie: `Bearer {twój_jwt_token}`
3. Kliknij **Authorize** w modal'u

## 📚 Funkcjonalności
- **Zarządzanie użytkownikami** - tworzenie, aktualizacja, deaktywacja kont
- **Zarządzanie działami** - organizacja struktury organizacyjnej
- **Zarządzanie zespołami** - tworzenie i konfiguracja Teams
- **Zarządzanie kanałami** - konfiguracja kanałów w zespołach
- **Szablony zespołów** - standardowe konfiguracje dla różnych typów szkół
- **Historia operacji** - audyt wszystkich działań w systemie

## 🏫 Typy szkół obsługiwane
- Szkoły podstawowe (SP)
- Licea ogólnokształcące (LO)  
- Technika (T)
- Szkoły branżowe (SB)

## 🔄 Wersjonowanie
API obsługuje wersjonowanie przez:
- Query string: `?version=1.0`
- Header: `X-Version: 1.0`
- URL segment: `/api/v1.0/controller`
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
    
    // Przygotowanie na przyszłe wersje
    options.SwaggerDoc("v2", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TeamsManager API",
        Version = "v2.0", 
        Description = @"
## 🚀 TeamsManager API v2.0 (Przyszła wersja)

**⚠️ Ta wersja jest w fazie planowania i nie jest jeszcze dostępna.**

### Planowane nowe funkcjonalności:
- **GraphQL Support** - alternatywny endpoint GraphQL
- **Webhooks** - powiadomienia o zmianach w czasie rzeczywistym  
- **Bulk Operations** - operacje masowe na dużych zestawach danych
- **Advanced Analytics** - rozszerzone raporty i metryki
- **Multi-tenant Support** - obsługa wielu organizacji
        ",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "TeamsManager Support",
            Email = "support@teamsmanager.local",
            Url = new Uri("https://github.com/teamsmanager/api")
        }
    });

    // Konfiguracja zabezpieczeń JWT Bearer
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = @"
## 🔐 Uwierzytelnianie JWT Bearer Token

Wprowadź **Bearer** [spacja] a następnie **token JWT** w polu tekstowym poniżej.

### Format:
```
Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Jak uzyskać token:
1. **Aplikacja WPF** - token jest automatycznie uzyskiwany przez MSAL
2. **Testowanie manualne** - użyj narzędzi jak Postman lub curl do uzyskania tokenu z Azure AD
3. **Development** - sprawdź logi aplikacji WPF dla przykładowego tokenu

### Uprawnienia wymagane:
- `User.Read` - odczyt podstawowych informacji użytkownika
- `Group.ReadWrite.All` - zarządzanie grupami/zespołami
- `Directory.ReadWrite.All` - zarządzanie strukturą organizacyjną

### Troubleshooting:
- **401 Unauthorized** - token wygasł lub jest nieprawidłowy
- **403 Forbidden** - brak wymaganych uprawnień
- **Error: Invalid audience** - token wystawiony dla innej aplikacji
        "
    });

    // Wymaganie tokenu Bearer dla wszystkich endpointów
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

    // Dodanie pliku XML z komentarzami
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
    
    // Dodanie XML komentarzy z TeamsManager.Core
    var coreXmlFile = "TeamsManager.Core.xml";
    var coreXmlPath = Path.Combine(AppContext.BaseDirectory, coreXmlFile);
    if (File.Exists(coreXmlPath))
    {
        options.IncludeXmlComments(coreXmlPath);
        Console.WriteLine($"✅ Swagger: Załadowano komentarze XML z Core: {coreXmlPath}");
    }

    // Konfiguracja schematów dla lepszej dokumentacji
    options.SchemaFilter<ExampleSchemaFilter>();
    options.OperationFilter<AuthorizationOperationFilter>();
    options.DocumentFilter<TagsDocumentFilter>();
    
    // Obsługa enum jako string zamiast int
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
// Dla SchoolType i Department używamy GenericRepository, ale można by stworzyć dedykowane, jeśli potrzebne
builder.Services.AddScoped<IGenericRepository<SchoolType>, GenericRepository<SchoolType>>();
builder.Services.AddScoped<IGenericRepository<Department>, GenericRepository<Department>>();
builder.Services.AddScoped<IGenericRepository<UserSchoolType>, GenericRepository<UserSchoolType>>();
builder.Services.AddScoped<IGenericRepository<UserSubject>, GenericRepository<UserSubject>>();

// Rejestracja Serwisów Aplikacyjnych
builder.Services.AddScoped<IPowerShellService, PowerShellService>();
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

// Rejestracja IConfidentialClientApplication dla przepływu On-Behalf-Of
builder.Services.AddScoped<IConfidentialClientApplication>(provider =>
{
    // Używamy już wczytanej konfiguracji oauthApiConfig
    // Authority powinno być w formacie: https://login.microsoftonline.com/{TenantId}
    // Instancja w konfiguracji (np. "https://login.microsoftonline.com/") powinna mieć na końcu ukośnik.
    var authority = $"{oauthApiConfig.AzureAd.Instance?.TrimEnd('/')}/{oauthApiConfig.AzureAd.TenantId}";

    // Logowanie konfiguracji używanej do stworzenia ConfidentialClientApplication
    var logger = provider.GetRequiredService<ILogger<Program>>(); // Pobieramy ILogger
    logger.LogInformation("Konfiguracja IConfidentialClientApplication: ClientId='{ApiAppClientId}', Authority='{Authority}', ClientSecret is set: {IsSecretSet}",
        oauthApiConfig.AzureAd.ClientId,
        authority,
        !string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.ClientSecret));


    return ConfidentialClientApplicationBuilder.Create(oauthApiConfig.AzureAd.ClientId)
        .WithClientSecret(oauthApiConfig.AzureAd.ClientSecret)
        .WithAuthority(new Uri(authority)) // Upewnij się, że authority jest poprawnym URI
        .Build();
});


const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options => {
    options.AddPolicy(name: MyAllowSpecificOrigins, policy => {
        // TODO: Dostosuj politykę CORS dla środowiska produkcyjnego!
        // W Development można zezwolić na wszystko dla ułatwienia.
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Dla produkcji:
            // policy.WithOrigins("https://twoja-domena-ui.com") // Zastąp rzeczywistą domeną UI
            //       .AllowAnyHeader()
            //       .AllowAnyMethod();
            // Lub bardziej restrykcyjnie, jeśli potrzeba.
            // Na razie dla testów można zostawić AllowAnyOrigin, ale pamiętaj o zmianie.
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Używamy Instance i TenantId z wczytanej konfiguracji
        options.Authority = $"{oauthApiConfig.AzureAd.Instance?.TrimEnd('/')}/{oauthApiConfig.AzureAd.TenantId}/v2.0";

        // Poprawiono: Audience powinno być App ID URI aplikacji API
        options.Audience = oauthApiConfig.AzureAd.Audience;

        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] // Akceptuj issuerów dla tokenów v1.0 i v2.0
            {
                $"{oauthApiConfig.AzureAd.Instance?.TrimEnd('/')}/{oauthApiConfig.AzureAd.TenantId}/v2.0",
                $"https://sts.windows.net/{oauthApiConfig.AzureAd.TenantId}/"
            },
            ValidateAudience = true, // Audience jest już ustawione wyżej i będzie walidowane
            // ValidAudience jest ustawiane przez opcję Audience powyżej, nie ma potrzeby ustawiać go tutaj ponownie.
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true, // Ważne dla bezpieczeństwa, wymaga konfiguracji kluczy (automatycznie z Authority)
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context => {
                // Logowanie błędów autentykacji
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
                // Można tu dodać własną logikę, jeśli standardowa odpowiedź 401 nie jest wystarczająca
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Przykład polityki, jeśli potrzebne bardziej szczegółowe reguły autoryzacji
    // np. options.AddPolicy("WymaganyClaimTenantId", policy => policy.RequireClaim("tid", oauthApiConfig.AzureAd.TenantId ?? throw new InvalidOperationException("TenantId is null in configuration for authorization policy.")));
});
// ----- KONIEC SEKCJI REJESTRACJI SERWISÓW -----

var app = builder.Build();

// ----- POCZĘTEK SEKCJI KONFIGURACJI HTTP PIPELINE -----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Dodaj różne wersje API do Swagger UI
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TeamsManager API v1.0");
        options.SwaggerEndpoint("/swagger/v2/swagger.json", "TeamsManager API v2.0");
        
        // Ustawienia UI
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
    app.UseHsts(); // Dodaj HSTS dla produkcji
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(MyAllowSpecificOrigins); // Użyj skonfigurowanej polityki CORS
app.UseAuthentication(); // Kluczowe dla działania [Authorize]
app.UseAuthorization();
app.MapControllers();
// ----- KONIEC SEKCJI KONFIGURACJI HTTP PIPELINE -----

app.Run();