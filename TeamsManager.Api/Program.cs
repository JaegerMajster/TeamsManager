using Microsoft.EntityFrameworkCore;
using TeamsManager.Data;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Services.UserContext;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Data.Repositories;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Services;
using TeamsManager.Core.Models;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// ----- POCZ¥TEK SEKCJI REJESTRACJI SERWISÓW -----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rejestracja DbContext
builder.Services.AddDbContext<TeamsManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Rejestracja ICurrentUserService
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// --- POCZ¥TEK: REJESTRACJA IMemoryCache ---
builder.Services.AddMemoryCache(); 
// --- KONIEC: REJESTRACJA IMemoryCache ---

// Rejestracja Repozytoriów
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<ITeamTemplateRepository, TeamTemplateRepository>();
builder.Services.AddScoped<ISchoolYearRepository, SchoolYearRepository>();
builder.Services.AddScoped<IOperationHistoryRepository, OperationHistoryRepository>();
builder.Services.AddScoped<IApplicationSettingRepository, ApplicationSettingRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();

// Dodatkowe generyczne repozytoria
builder.Services.AddScoped<IGenericRepository<SchoolType>, GenericRepository<SchoolType>>();
builder.Services.AddScoped<IGenericRepository<Department>, GenericRepository<Department>>(); // Dla UserService
builder.Services.AddScoped<IGenericRepository<UserSchoolType>, GenericRepository<UserSchoolType>>(); // Dla UserService
builder.Services.AddScoped<IGenericRepository<UserSubject>, GenericRepository<UserSubject>>();   // Dla UserService

// ===== REJESTRACJA SERWISU APLIKACYJNEGO =====
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


// ----- KONIEC SEKCJI REJESTRACJI SERWISÓW -----

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization(); // Jeœli bêdziesz u¿ywaæ autoryzacji
app.MapControllers();

app.Run();