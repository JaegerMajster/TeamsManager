using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Microsoft.OpenApi.Any;

namespace TeamsManager.Api.Swagger
{
    /// <summary>
    /// Filtr dodający przykłady do schematów w dokumentacji Swagger
    /// </summary>
    public class ExampleSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == null) return;

            // Przykłady dla głównych modeli
            if (context.Type == typeof(User))
            {
                schema.Example = new OpenApiObject
                {
                    ["id"] = new OpenApiString("user-123-456"),
                    ["firstName"] = new OpenApiString("Jan"),
                    ["lastName"] = new OpenApiString("Kowalski"),
                    ["upn"] = new OpenApiString("jan.kowalski@szkola.edu.pl"),
                    ["role"] = new OpenApiString("Teacher"),
                    ["departmentId"] = new OpenApiString("dept-it-001"),
                    ["phone"] = new OpenApiString("+48 123 456 789"),
                    ["alternateEmail"] = new OpenApiString("j.kowalski@prywatny.pl"),
                    ["position"] = new OpenApiString("Nauczyciel informatyki"),
                    ["isActive"] = new OpenApiBoolean(true),
                    ["isSystemAdmin"] = new OpenApiBoolean(false)
                };
            }
            else if (context.Type == typeof(Department))
            {
                schema.Example = new OpenApiObject
                {
                    ["id"] = new OpenApiString("dept-it-001"),
                    ["name"] = new OpenApiString("Informatyka"),
                    ["description"] = new OpenApiString("Wydział Informatyki i Technologii"),
                    ["departmentCode"] = new OpenApiString("IT"),
                    ["email"] = new OpenApiString("informatyka@szkola.edu.pl"),
                    ["phone"] = new OpenApiString("+48 123 456 700"),
                    ["location"] = new OpenApiString("Budynek A, piętro 2"),
                    ["sortOrder"] = new OpenApiInteger(10),
                    ["isActive"] = new OpenApiBoolean(true)
                };
            }
            else if (context.Type == typeof(Team))
            {
                schema.Example = new OpenApiObject
                {
                    ["id"] = new OpenApiString("team-1a-2024"),
                    ["displayName"] = new OpenApiString("Klasa 1A - Rok szkolny 2024/2025"),
                    ["description"] = new OpenApiString("Zespół dla klasy 1A szkoły podstawowej"),
                    ["mailNickname"] = new OpenApiString("klasa-1a-2024"),
                    ["teamsId"] = new OpenApiString("19:abcd1234-5678-90ef-ghij-klmnopqrstuv@thread.tacv2"),
                    ["status"] = new OpenApiString("Active"),
                    ["schoolYearId"] = new OpenApiString("sy-2024-2025"),
                    ["schoolTypeId"] = new OpenApiString("st-primary"),
                    ["isActive"] = new OpenApiBoolean(true)
                };
            }
            else if (context.Type == typeof(SchoolType))
            {
                schema.Example = new OpenApiObject
                {
                    ["id"] = new OpenApiString("st-primary"),
                    ["shortName"] = new OpenApiString("SP"),
                    ["fullName"] = new OpenApiString("Szkoła Podstawowa"),
                    ["description"] = new OpenApiString("Szkoła podstawowa dla uczniów klas I-VIII"),
                    ["colorCode"] = new OpenApiString("#4CAF50"),
                    ["sortOrder"] = new OpenApiInteger(1),
                    ["isActive"] = new OpenApiBoolean(true)
                };
            }
            else if (context.Type == typeof(Channel))
            {
                schema.Example = new OpenApiObject
                {
                    ["id"] = new OpenApiString("channel-general-001"),
                    ["teamId"] = new OpenApiString("team-1a-2024"),
                    ["displayName"] = new OpenApiString("Ogólny"),
                    ["description"] = new OpenApiString("Główny kanał komunikacji klasy"),
                    ["channelType"] = new OpenApiString("Standard"),
                    ["teamsChannelId"] = new OpenApiString("19:xyz789abc-def0-1234-5678-9abcdefghijk@thread.tacv2"),
                    ["isGeneral"] = new OpenApiBoolean(true),
                    ["isPrivate"] = new OpenApiBoolean(false),
                    ["isActive"] = new OpenApiBoolean(true)
                };
            }

            // Przykłady dla DTO
            AddDtoExamples(schema, context);
        }

        private void AddDtoExamples(OpenApiSchema schema, SchemaFilterContext context)
        {
            var typeName = context.Type.Name;

            if (typeName.Contains("CreateUserRequestDto"))
            {
                schema.Example = new OpenApiObject
                {
                    ["firstName"] = new OpenApiString("Anna"),
                    ["lastName"] = new OpenApiString("Nowak"),
                    ["upn"] = new OpenApiString("anna.nowak@szkola.edu.pl"),
                    ["password"] = new OpenApiString("TajneHaslo123!"),
                    ["role"] = new OpenApiString("Teacher"),
                    ["departmentId"] = new OpenApiString("dept-math-001"),
                    ["phone"] = new OpenApiString("+48 123 456 789"),
                    ["position"] = new OpenApiString("Nauczyciel matematyki")
                };
            }
            else if (typeName.Contains("CreateDepartmentRequestDto"))
            {
                schema.Example = new OpenApiObject
                {
                    ["name"] = new OpenApiString("Matematyka"),
                    ["description"] = new OpenApiString("Wydział Matematyki i Fizyki"),
                    ["departmentCode"] = new OpenApiString("MATH"),
                    ["email"] = new OpenApiString("matematyka@szkola.edu.pl"),
                    ["phone"] = new OpenApiString("+48 123 456 701"),
                    ["location"] = new OpenApiString("Budynek B, piętro 1")
                };
            }
            else if (typeName.Contains("CreateTeamRequestDto"))
            {
                schema.Example = new OpenApiObject
                {
                    ["displayName"] = new OpenApiString("Klasa 2B - Rok szkolny 2024/2025"),
                    ["description"] = new OpenApiString("Zespół dla klasy 2B liceum ogólnokształcącego"),
                    ["mailNickname"] = new OpenApiString("klasa-2b-2024"),
                    ["schoolYearId"] = new OpenApiString("sy-2024-2025"),
                    ["schoolTypeId"] = new OpenApiString("st-highschool"),
                    ["templateId"] = new OpenApiString("template-highschool-standard")
                };
            }
            else if (typeName.Contains("CreateSchoolTypeRequestDto"))
            {
                schema.Example = new OpenApiObject
                {
                    ["shortName"] = new OpenApiString("T"),
                    ["fullName"] = new OpenApiString("Technikum"),
                    ["description"] = new OpenApiString("Szkoła techniczna dla uczniów klas I-V"),
                    ["colorCode"] = new OpenApiString("#FF9800"),
                    ["sortOrder"] = new OpenApiInteger(3)
                };
            }
        }
    }
} 