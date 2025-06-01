// Plik: TeamsManager.Api/Program.cs

using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// --- POCZĘTEK SEKCJI DEFINICJI POMOCNICZYCH (PRZED TOP-LEVEL STATEMENTS) ---



// --- KONIEC SEKCJI DEFINICJI POMOCNICZYCH ---

var builder = WebApplication.CreateBuilder(args);

// Wczytaj konfigurację OAuth na samym początku
var oauthApiConfig = ApiAuthConfig.LoadApiOAuthConfig(builder.Configuration);

if (string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.TenantId) || string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.ClientId))
{
    var errorMessage = "[KRYTYCZNY BŁĄD KONFIGURACJI] TenantId lub ClientId (Audience) dla AzureAd nie zostały poprawnie załadowane. Uwierzytelnianie JWT nie będzie działać. Sprawdź konfigurację.";
    Console.WriteLine(errorMessage);
    System.Diagnostics.Debug.WriteLine(errorMessage);
    // W tym momencie można by rzucić wyjątek, aby zatrzymać start aplikacji, np.:
    // throw new InvalidOperationException(errorMessage);
}

// ----- POCZĘTEK SEKCJI REJESTRACJI SERWISÓW -----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TeamsManager API",
        Version = "v1.0",
        Description = "API dla aplikacji TeamsManager - zarządzanie zespołami Microsoft Teams"
    });

    // Dodanie obsługi autoryzacji Bearer JWT w Swagger UI
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Wprowadź 'Bearer' [spacja] a następnie token JWT w polu tekstowym poniżej.\r\n\r\nPrzykład: \"Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...\""
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

const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options => {
    options.AddPolicy(name: MyAllowSpecificOrigins, policy => {
        if (builder.Environment.IsDevelopment()) { policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); }
        else { policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); /* TODO: Produkcja */ }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Konfiguracja dla Azure AD v2.0 (teraz UI będzie generować tokeny v2.0)
        options.Authority = $"https://login.microsoftonline.com/{oauthApiConfig.AzureAd.TenantId}/v2.0";
        // Token jest wydany dla Microsoft Graph API
        options.Audience = oauthApiConfig.AzureAd.ClientId;

        // W środowisku development wyłączamy wymóg HTTPS dla metadata
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // Akceptuj oba możliwe issuery (v1.0 i v2.0)
            ValidIssuers = new[]
            {
                $"https://login.microsoftonline.com/{oauthApiConfig.AzureAd.TenantId}/v2.0",
                $"https://sts.windows.net/{oauthApiConfig.AzureAd.TenantId}/"
            },
            ValidateAudience = true,
            ValidAudience = $"api://{oauthApiConfig.AzureAd.ClientId}", // "api://5ee301dd-6049-4b36-959c-b89ff4b05b32"
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)  // Tolerancja na różnice czasu
        };
        
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context => {
                Console.WriteLine($"BŁĄD autentykacji (API): {context.Exception.Message}");
                Console.WriteLine($"Token: {context.Request.Headers.Authorization}");
                System.Diagnostics.Debug.WriteLine($"BŁĄD autentykacji (API): {context.Exception.ToString()}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context => {
                Console.WriteLine($"✅ Token pomyślnie zwalidowany (API) dla użytkownika: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            },
            OnChallenge = context => {
                Console.WriteLine($"JWT Challenge: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    if (!string.IsNullOrWhiteSpace(oauthApiConfig.AzureAd.TenantId))
    {
        // options.AddPolicy("TylkoMojTenant", policy => policy.RequireClaim("tid", oauthApiConfig.AzureAd.TenantId));
    }
});
// ----- KONIEC SEKCJI REJESTRACJI SERWISÓW -----

var app = builder.Build();

// ----- POCZĘTEK SEKCJI KONFIGURACJI HTTP PIPELINE -----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
// ----- KONIEC SEKCJI KONFIGURACJI HTTP PIPELINE -----

app.Run();