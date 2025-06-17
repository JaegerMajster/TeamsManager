using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TeamsManager.UI.Models;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services
{
    public class ManualTestingService : IManualTestingService
    {
        private readonly string _resultsFilePath;
        private readonly string _configFilePath;

        public ManualTestingService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "TeamsManager");
            Directory.CreateDirectory(appFolder);
            
            _resultsFilePath = Path.Combine(appFolder, "WynikiTestowManualnych.md");
            _configFilePath = Path.Combine(appFolder, "manual_tests_config.json");
        }

        public TestSuite CreateDefaultTestSuite()
        {
            var suite = new TestSuite();
            
            // 1. Testy Kompilacji i Uruchomienia
            suite.TestCases.AddRange(new[]
            {
                new TestCase
                {
                    Id = "COMP-001",
                    Category = "Kompilacja",
                    Name = "Kompilacja API",
                    Description = "Test kompilacji projektu TeamsManager.Api",
                    Steps = new List<string>
                    {
                        "Otwórz terminal w katalogu projektu",
                        "Wykonaj: dotnet build TeamsManager.Api"
                    },
                    ExpectedResult = "✅ 0 errors, 0 warnings",
                    Priority = "High"
                },
                new TestCase
                {
                    Id = "COMP-002", 
                    Category = "Kompilacja",
                    Name = "Kompilacja UI",
                    Description = "Test kompilacji projektu TeamsManager.UI",
                    Steps = new List<string>
                    {
                        "Otwórz terminal w katalogu projektu",
                        "Wykonaj: dotnet build TeamsManager.UI"
                    },
                    ExpectedResult = "✅ 0 errors, 0 warnings",
                    Priority = "High"
                },
                new TestCase
                {
                    Id = "RUN-001",
                    Category = "Uruchomienie",
                    Name = "Uruchomienie API",
                    Description = "Test uruchomienia API na HTTPS",
                    Steps = new List<string>
                    {
                        "Wykonaj: dotnet run --project TeamsManager.Api --launch-profile https",
                        "Sprawdź logi w konsoli"
                    },
                    ExpectedResult = "✅ Listening on: https://localhost:7037 i http://localhost:5182",
                    Priority = "High",
                    ApiEndpoint = "https://localhost:7037"
                },
                new TestCase
                {
                    Id = "RUN-002",
                    Category = "Uruchomienie", 
                    Name = "Uruchomienie UI",
                    Description = "Test uruchomienia aplikacji WPF",
                    Steps = new List<string>
                    {
                        "Wykonaj: dotnet run --project TeamsManager.UI",
                        "Sprawdź czy okno się otwiera"
                    },
                    ExpectedResult = "✅ Aplikacja WPF uruchamia się bez błędów",
                    Priority = "High"
                }
            });

            // 2. Testy Autentykacji
            suite.TestCases.AddRange(new[]
            {
                new TestCase
                {
                    Id = "AUTH-001",
                    Category = "Autentykacja",
                    Name = "Publiczny endpoint API",
                    Description = "Test dostępu do publicznego endpointu",
                    Steps = new List<string>
                    {
                        "Otwórz przeglądarkę lub Postman",
                        "Wykonaj GET https://localhost:7037/api/TestAuth/publicinfo"
                    },
                    ExpectedResult = "✅ Zwraca JSON z message i timestamp",
                    Priority = "High",
                    ApiEndpoint = "https://localhost:7037/api/TestAuth/publicinfo",
                    IsAutomatable = true
                },
                new TestCase
                {
                    Id = "AUTH-002",
                    Category = "Autentykacja",
                    Name = "Endpoint wymagający autoryzacji - bez tokena",
                    Description = "Test blokady dostępu bez tokena",
                    Steps = new List<string>
                    {
                        "Wykonaj GET https://localhost:7037/api/TestAuth/whoami",
                        "Bez nagłówka Authorization"
                    },
                    ExpectedResult = "❌ 401 Unauthorized",
                    Priority = "High",
                    ApiEndpoint = "https://localhost:7037/api/TestAuth/whoami",
                    IsAutomatable = true
                },
                new TestCase
                {
                    Id = "AUTH-003",
                    Category = "Autentykacja",
                    Name = "Logowanie w UI",
                    Description = "Test logowania przez Azure AD w aplikacji",
                    Steps = new List<string>
                    {
                        "Uruchom TeamsManager.UI",
                        "Kliknij przycisk 'Zaloguj się'",
                        "Zaloguj się przez Azure AD"
                    },
                    ExpectedResult = "✅ Pomyślne zalogowanie, token widoczny",
                    Priority = "High"
                },
                new TestCase
                {
                    Id = "AUTH-004",
                    Category = "Autentykacja",
                    Name = "Endpoint z prawidłowym tokenem",
                    Description = "Test autoryzowanego dostępu",
                    Steps = new List<string>
                    {
                        "Uzyskaj token z UI",
                        "Wykonaj GET https://localhost:7037/api/TestAuth/whoami",
                        "Z nagłówkiem: Authorization: Bearer [token]"
                    },
                    ExpectedResult = "✅ Zwraca dane użytkownika (UPN, ID, claims)",
                    Priority = "High",
                    ApiEndpoint = "https://localhost:7037/api/TestAuth/whoami"
                }
            });

            // 3. Testy Komunikacji UI ↔ API
            suite.TestCases.AddRange(new[]
            {
                new TestCase
                {
                    Id = "COMM-001",
                    Category = "Komunikacja",
                    Name = "Ładowanie użytkowników w UI",
                    Description = "Test komunikacji UI z API dla użytkowników",
                    Steps = new List<string>
                    {
                        "W UI przejdź do sekcji 'Użytkownicy'",
                        "Sprawdź czy lista się ładuje"
                    },
                    ExpectedResult = "✅ Lista użytkowników lub komunikat o braku danych",
                    Priority = "Medium"
                },
                new TestCase
                {
                    Id = "COMM-002",
                    Category = "Komunikacja",
                    Name = "Ładowanie zespołów w UI",
                    Description = "Test komunikacji UI z API dla zespołów",
                    Steps = new List<string>
                    {
                        "W UI przejdź do sekcji 'Teams'",
                        "Sprawdź czy lista się ładuje"
                    },
                    ExpectedResult = "✅ Lista zespołów lub komunikat o braku danych",
                    Priority = "Medium"
                },
                new TestCase
                {
                    Id = "COMM-003",
                    Category = "Komunikacja",
                    Name = "Obsługa błędu połączenia",
                    Description = "Test reakcji UI na brak API",
                    Steps = new List<string>
                    {
                        "Zatrzymaj API (Ctrl+C)",
                        "W UI spróbuj załadować dane"
                    },
                    ExpectedResult = "❌ Czytelny komunikat o błędzie połączenia",
                    Priority = "Medium"
                }
            });

            // 4. Testy Graph API Diagnostics
            suite.TestCases.AddRange(new[]
            {
                new TestCase
                {
                    Id = "GRAPH-DIAG-001",
                    Category = "Graph API",
                    Name = "Test statusu Graph API",
                    Description = "Test stanu połączenia Graph API",
                    Steps = new List<string>
                    {
                        "Wykonaj GET https://localhost:7037/api/diagnostics/graph/status",
                        "Z nagłówkiem Authorization: Bearer [token]"
                    },
                    ExpectedResult = "Zwraca status połączenia Graph API i diagnostyki",
                    Priority = "Medium",
                    ApiEndpoint = "https://localhost:7037/api/diagnostics/graph/status"
                },
                new TestCase
                {
                    Id = "GRAPH-DIAG-002",
                    Category = "Graph API",
                    Name = "Test uprawnień Graph API",
                    Description = "Test uprawnień w Microsoft Graph",
                    Steps = new List<string>
                    {
                        "Wykonaj GET https://localhost:7037/api/diagnostics/graph/permissions",
                        "Z nagłówkiem Authorization: Bearer [token]"
                    },
                    ExpectedResult = "✅ Lista uprawnień Graph API LUB ❌ Brak uprawnień",
                    Priority = "High",
                    ApiEndpoint = "https://localhost:7037/api/diagnostics/graph/permissions"
                }
            });

            // 5. Testy Microsoft Graph
            suite.TestCases.AddRange(new[]
            {
                new TestCase
                {
                    Id = "GRAPH-001",
                    Category = "Microsoft Graph",
                    Name = "Test podstawowy Graph API",
                    Description = "Test połączenia z Microsoft Graph",
                    Steps = new List<string>
                    {
                        "Wykonaj GET https://localhost:7037/api/Users/graph-test",
                        "Z nagłówkiem Authorization: Bearer [token]"
                    },
                    ExpectedResult = "✅ Zwraca dane aktualnego użytkownika z Graph",
                    Priority = "High",
                    ApiEndpoint = "https://localhost:7037/api/Users/graph-test"
                },
                new TestCase
                {
                    Id = "GRAPH-002",
                    Category = "Microsoft Graph",
                    Name = "Lista zespołów użytkownika",
                    Description = "Test pobierania zespołów z Graph",
                    Steps = new List<string>
                    {
                        "Wykonaj GET https://localhost:7037/api/Teams/my-teams",
                        "Z nagłówkiem Authorization: Bearer [token]"
                    },
                    ExpectedResult = "✅ Lista zespołów użytkownika lub pusta lista []",
                    Priority = "Medium",
                    ApiEndpoint = "https://localhost:7037/api/Teams/my-teams"
                }
            });

            // 6. Test End-to-End
            suite.TestCases.Add(new TestCase
            {
                Id = "E2E-001",
                Category = "End-to-End",
                Name = "Tworzenie zespołu - pełny przepływ",
                Description = "Test kompletnego przepływu tworzenia zespołu",
                Steps = new List<string>
                {
                    "Zaloguj się w UI",
                    "Przejdź do 'Nowy Zespół'",
                    "Wypełnij formularz: Nazwa='Test Team E2E', Opis='Test end-to-end', Typ='Private'",
                    "Kliknij 'Utwórz'",
                    "Sprawdź rezultat"
                },
                ExpectedResult = "✅ Zespół utworzony w Microsoft Teams LUB ❌ Czytelny komunikat błędu",
                Priority = "High"
            });

            return suite;
        }

        public async Task SaveTestResults(TestSuite testSuite)
        {
            var markdown = GenerateMarkdownReport(testSuite);
            await File.WriteAllTextAsync(_resultsFilePath, markdown, Encoding.UTF8);
        }

        public async Task<TestSuite?> LoadTestResults()
        {
            if (!File.Exists(_configFilePath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                return JsonSerializer.Deserialize<TestSuite>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveTestConfig(TestSuite testSuite)
        {
            var json = JsonSerializer.Serialize(testSuite, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json);
        }

        private string GenerateMarkdownReport(TestSuite testSuite)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# 📋 Wyniki Testów Manualnych - TeamsManager");
            sb.AppendLine();
            sb.AppendLine($"**Data wykonania:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Wersja:** {testSuite.Version}");
            sb.AppendLine();
            
            // Podsumowanie
            sb.AppendLine("## 📊 Podsumowanie");
            sb.AppendLine();
            sb.AppendLine("| Metryka | Wartość |");
            sb.AppendLine("|---------|---------|");
            sb.AppendLine($"| Łączna liczba testów | {testSuite.TotalTests} |");
            sb.AppendLine($"| ✅ Przeszły | {testSuite.PassedTests} |");
            sb.AppendLine($"| ❌ Niepowodzenia | {testSuite.FailedTests} |");
            sb.AppendLine($"| ⚠️ Ostrzeżenia | {testSuite.WarningTests} |");
            sb.AppendLine($"| ⏭️ Pominięte | {testSuite.SkippedTests} |");
            sb.AppendLine($"| ⏸️ Nie uruchamiane | {testSuite.NotRunTests} |");
            sb.AppendLine($"| 📈 Wskaźnik sukcesu | {testSuite.SuccessRate:F1}% |");
            sb.AppendLine();

            // Testy po kategoriach
            var categories = testSuite.TestCases.GroupBy(t => t.Category).OrderBy(g => g.Key);
            
            foreach (var category in categories)
            {
                sb.AppendLine($"## 🔍 {category.Key}");
                sb.AppendLine();
                
                foreach (var test in category.OrderBy(t => t.Id))
                {
                    var resultIcon = test.Result switch
                    {
                        TestResult.Pass => "✅",
                        TestResult.Fail => "❌", 
                        TestResult.Warning => "⚠️",
                        TestResult.Skip => "⏭️",
                        _ => "⏸️"
                    };
                    
                    sb.AppendLine($"### {resultIcon} {test.Id}: {test.Name}");
                    sb.AppendLine();
                    sb.AppendLine($"**Opis:** {test.Description}");
                    sb.AppendLine();
                    sb.AppendLine("**Kroki:**");
                    foreach (var step in test.Steps)
                    {
                        sb.AppendLine($"1. {step}");
                    }
                    sb.AppendLine();
                    sb.AppendLine($"**Oczekiwany rezultat:** {test.ExpectedResult}");
                    sb.AppendLine();
                    sb.AppendLine($"**Status:** {test.Result}");
                    
                    if (test.ExecutedAt.HasValue)
                    {
                        sb.AppendLine($"**Wykonano:** {test.ExecutedAt:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    if (!string.IsNullOrEmpty(test.Notes))
                    {
                        sb.AppendLine($"**Notatki:** {test.Notes}");
                    }
                    
                    if (!string.IsNullOrEmpty(test.ErrorDetails))
                    {
                        sb.AppendLine($"**Szczegóły błędu:**");
                        sb.AppendLine("```");
                        sb.AppendLine(test.ErrorDetails);
                        sb.AppendLine("```");
                    }
                    
                    if (!string.IsNullOrEmpty(test.ApiEndpoint))
                    {
                        sb.AppendLine($"**Endpoint:** `{test.ApiEndpoint}`");
                    }
                    
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
            }
            
            sb.AppendLine("## 📝 Notatki");
            sb.AppendLine();
            sb.AppendLine("*Raport wygenerowany automatycznie przez TeamsManager Test Suite*");
            
            return sb.ToString();
        }

        public string GetResultsFilePath() => _resultsFilePath;
    }
} 