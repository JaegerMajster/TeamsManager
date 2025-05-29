using Microsoft.EntityFrameworkCore;
using TeamsManager.Data;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Services.UserContext;

var builder = WebApplication.CreateBuilder(args);

// Dodanie serwisów do kontenera
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rejestracja DbContext
builder.Services.AddDbContext<TeamsManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Tymczasowa rejestracja ICurrentUserService
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build(); // Linia budowania aplikacji

// Konfiguracja HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();