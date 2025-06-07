using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TeamsManager.Api.Swagger
{
    /// <summary>
    /// Filtr organizujący tagi kontrolerów w dokumentacji Swagger
    /// </summary>
    public class TagsDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            // Definiowanie tagów z opisami i ikonami
            var tagDescriptions = new Dictionary<string, (string Description, int Order)>
            {
                ["Users"] = ("👥 **Zarządzanie użytkownikami**\n\nOperacje CRUD dla użytkowników systemu, zarządzanie rolami i uprawnieniami.", 1),
                ["Departments"] = ("🏢 **Zarządzanie działami**\n\nOrganizacja struktury organizacyjnej, hierarchia działów i przypisania użytkowników.", 2),
                ["Teams"] = ("👨‍👩‍👧‍👦 **Zarządzanie zespołami**\n\nTworzenie i konfiguracja zespołów Microsoft Teams, zarządzanie członkami zespołu.", 3),
                ["Channels"] = ("💬 **Zarządzanie kanałami**\n\nKonfiguracja kanałów komunikacji w zespołach, ustawienia prywatności i uprawnień.", 4),
                ["SchoolTypes"] = ("🏫 **Typy szkół**\n\nDefinicje typów placówek edukacyjnych (SP, LO, Technikum, Szkoła Branżowa).", 5),
                ["SchoolYears"] = ("📅 **Lata szkolne**\n\nZarządzanie okresami nauczania i cyklami edukacyjnymi.", 6),
                ["Subjects"] = ("📚 **Przedmioty**\n\nKatalog przedmiotów nauczania i ich konfiguracja.", 7),
                ["TeamTemplates"] = ("📋 **Szablony zespołów**\n\nPredefinowane konfiguracje zespołów dla różnych typów szkół i celów edukacyjnych.", 8),
                ["ApplicationSettings"] = ("⚙️ **Ustawienia aplikacji**\n\nGlobalne parametry konfiguracyjne systemu i opcje personalizacji.", 9),
                ["OperationHistories"] = ("📊 **Historia operacji**\n\nAudyt działań w systemie, śledzenie zmian i raportowanie.", 10),
                ["TestAuth"] = ("🔧 **Testowanie uwierzytelniania**\n\nEndpointy pomocnicze do testowania mechanizmów autoryzacji w środowisku deweloperskim.", 11),
                ["PowerShell"] = ("⚡ **Operacje PowerShell**\n\nWykonywanie skryptów PowerShell do zaawansowanego zarządzania Microsoft 365.", 12)
            };

            // Jeśli dokument nie ma tagów, utwórz listę
            if (swaggerDoc.Tags == null)
            {
                swaggerDoc.Tags = new List<OpenApiTag>();
            }

            // Dodaj lub zaktualizuj tagi
            foreach (var (tagName, (description, order)) in tagDescriptions)
            {
                var existingTag = swaggerDoc.Tags.FirstOrDefault(t => t.Name == tagName);
                if (existingTag != null)
                {
                    existingTag.Description = description;
                }
                else
                {
                    swaggerDoc.Tags.Add(new OpenApiTag
                    {
                        Name = tagName,
                        Description = description
                    });
                }
            }

            // Sortuj tagi według zdefiniowanej kolejności
            swaggerDoc.Tags = swaggerDoc.Tags
                .OrderBy(tag => tagDescriptions.ContainsKey(tag.Name) 
                    ? tagDescriptions[tag.Name].Order 
                    : 999)
                .ToList();

            // Dodaj informacje o serwerach API
            if (swaggerDoc.Servers?.Any() != true)
            {
                swaggerDoc.Servers = new List<OpenApiServer>
                {
                    new OpenApiServer
                    {
                        Url = "https://localhost:7037",
                        Description = "🚀 Development Server (HTTPS)"
                    },
                    new OpenApiServer
                    {
                        Url = "http://localhost:5182", 
                        Description = "🔧 Development Server (HTTP)"
                    }
                };
            }

            // Dodaj dodatkowe informacje do metadanych dokumentu
            if (swaggerDoc.Info != null)
            {
                // Sprawdź czy klucz już istnieje przed dodaniem
                if (!swaggerDoc.Info.Extensions.ContainsKey("x-logo"))
                {
                    swaggerDoc.Info.Extensions.Add("x-logo", new Microsoft.OpenApi.Any.OpenApiObject
                    {
                        ["url"] = new Microsoft.OpenApi.Any.OpenApiString("/swagger-ui/favicon-32x32.png"),
                        ["altText"] = new Microsoft.OpenApi.Any.OpenApiString("TeamsManager API")
                    });
                }

                // Dodaj informacje o autorach i wersji (z sprawdzeniem)
                if (!swaggerDoc.Info.Extensions.ContainsKey("x-api-id"))
                {
                    swaggerDoc.Info.Extensions.Add("x-api-id", new Microsoft.OpenApi.Any.OpenApiString("teamsmanager-api"));
                }
                if (!swaggerDoc.Info.Extensions.ContainsKey("x-audience"))
                {
                    swaggerDoc.Info.Extensions.Add("x-audience", new Microsoft.OpenApi.Any.OpenApiString("Educational institutions using Microsoft 365"));
                }
            }

            // Ustaw zewnętrzną dokumentację
            swaggerDoc.ExternalDocs = new OpenApiExternalDocs
            {
                Description = "📖 Dokumentacja TeamsManager na GitHub",
                Url = new Uri("https://github.com/teamsmanager/documentation")
            };
        }
    }
} 