using Microsoft.EntityFrameworkCore;
using TeamsManager.Data;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Services.UserContext;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ----- POCZ�TEK SEKCJI REJESTRACJI SERWIS�W -----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rejestracja DbContext
builder.Services.AddDbContext<TeamsManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Rejestracja ICurrentUserService
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ===== REJESTRACJA REPOZYTORI�W =====
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<ITeamTemplateRepository, TeamTemplateRepository>();
builder.Services.AddScoped<ISchoolYearRepository, SchoolYearRepository>();
builder.Services.AddScoped<IOperationHistoryRepository, OperationHistoryRepository>();
builder.Services.AddScoped<IApplicationSettingRepository, ApplicationSettingRepository>();

// ----- KONIEC SEKCJI REJESTRACJI SERWIS�W -----

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization(); // Je�li b�dziesz u�ywa� autoryzacji
app.MapControllers();

app.Run();