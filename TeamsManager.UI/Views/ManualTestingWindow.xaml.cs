using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TeamsManager.UI.Models;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Abstractions;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TeamsManager.UI.Views
{
    public partial class ManualTestingWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly IManualTestingService _testingService;
        private readonly IMsalAuthService _msalAuthService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ManualTestingWindow> _logger;
        private TestSuite _currentTestSuite = null!;
        private TestCase? _selectedTestCase;
        private AuthenticationResult? _authResult;
        private HttpClient? _httpClient;
        private readonly DateTime _sessionStartTime;

        // Właściwość do sprawdzania czy okno jest zamknięte
        public bool IsClosed { get; private set; } = false;

        /// <summary>
        /// Konstruktor okna testów manualnych z dependency injection
        /// </summary>
        /// <param name="msalAuthService">Serwis autentykacji MSAL</param>
        /// <param name="manualTestingService">Serwis zarządzania testami</param>
        /// <param name="httpClientFactory">Fabryka klientów HTTP</param>
        /// <param name="logger">Logger dla diagnostyki</param>
        public ManualTestingWindow(
            IMsalAuthService msalAuthService,
            IManualTestingService manualTestingService,
            IHttpClientFactory httpClientFactory,
            ILogger<ManualTestingWindow> logger)
        {
            InitializeComponent();
            
            _msalAuthService = msalAuthService ?? throw new ArgumentNullException(nameof(msalAuthService));
            _testingService = manualTestingService ?? throw new ArgumentNullException(nameof(manualTestingService));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _sessionStartTime = DateTime.Now;

            // Ustaw ciemny motyw dla tego okna
            this.SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                SetWindowToDarkMode(helper.Handle);
            };

            // Obsługa zamknięcia okna - automatyczne generowanie raportu
            this.Closing += async (s, e) =>
            {
                await GenerateSessionReportOnCloseAsync();
                IsClosed = true;
            };

            // Informacje o użytkowniku będą ustawione przez SetAuthenticationContext
            UserInfoText.Text = "Użytkownik: Inicjalizacja...";
            SessionInfoText.Text = $"Sesja rozpoczęta: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}";

            // Inicjalizacja serwisów i testów
            InitializeTestingServices();
            LoadDefaultTests();
        }

        /// <summary>
        /// Inicjalizuje serwisy testowe i sprawdza ich dostępność
        /// </summary>
        private void InitializeTestingServices()
        {
            try
            {
                // Utworzenie HttpClient - używamy default client bez specjalnej konfiguracji
                // aby móc przełączać tokeny podczas testów
                _httpClient = _httpClientFactory.CreateClient();
                _logger.LogDebug("ManualTestingWindow: HttpClient utworzony przez factory");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas inicjalizacji serwisów testowych");
                MessageBox.Show(
                    "Nie udało się zainicjalizować serwisów testowych.\nNiektóre funkcje mogą być niedostępne.",
                    "Ostrzeżenie",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Ustawia kontekst uwierzytelniania po utworzeniu okna
        /// </summary>
        /// <param name="authResult">Wynik uwierzytelniania</param>
        public void SetAuthenticationContext(AuthenticationResult? authResult)
        {
            _authResult = authResult;
            
            // Aktualizuj informacje o użytkowniku
            if (_authResult?.Account?.Username != null)
            {
                UserInfoText.Text = $"Użytkownik: {_authResult.Account.Username}";
                SessionInfoText.Text = $"Sesja rozpoczęta: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}";
            }
            else
            {
                UserInfoText.Text = "Użytkownik: Niezalogowany";
                SessionInfoText.Text = $"Sesja rozpoczęta: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private static void SetWindowToDarkMode(IntPtr handle)
        {
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch
            {
                // Jeśli nie działa na starszej wersji Windows, po prostu ignoruj
            }
        }

        private void LoadDefaultTests()
        {
            _logger.LogDebug("Ładowanie domyślnych testów...");
            try
            {
                _currentTestSuite = new TestSuite
                {
                    Name = "Testy Manualne TeamsManager",
                    Version = "1.0",
                    TestCases = new List<TestCase>()
                };

                // Dodaj testy według kategorii
                AddAuthenticationTests();
                AddGraphApiTests();
                AddPowerShellTests();
                AddTeamsManagementTests();
                AddUITests();

                // Załaduj testy do kategorycznych list
                LoadTestsToCategories();
                UpdateStatistics();
                
                _logger.LogInformation("Załadowano {Count} testów", _currentTestSuite.TestCases.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania testów");
            }
        }

        private void AddAuthenticationTests()
        {
            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "AUTH-001",
                Name = "Test logowania do systemu",
                Description = "Weryfikuje poprawność procesu uwierzytelniania użytkownika w aplikacji TeamsManager. " +
                             "Test sprawdza czy aplikacja może pomyślnie uwierzytelnić użytkownika za pomocą Microsoft Identity Platform " +
                             "i uzyskać niezbędne tokeny dostępu do lokalnego API.",
                Category = "Uwierzytelnianie",
                Priority = "Krytyczny",
                ExpectedResult = "Użytkownik zostanie pomyślnie uwierzytelniony, aplikacja otrzyma token dostępu i wyświetli informacje o użytkowniku",
                Steps = new List<string>
                {
                    "Uruchom aplikację TeamsManager",
                    "Kliknij przycisk 'Zaloguj się'",
                    "Wprowadź poprawne dane logowania Microsoft",
                    "Potwierdź zgody na dostęp aplikacji (jeśli wymagane)",
                    "Sprawdź czy aplikacja wyświetla nazwę użytkownika i awatar",
                    "Sprawdź czy przycisk 'Testy manualne' został aktywowany"
                },
                HasAutomaticExecution = true,
                AutoExecuteButtonText = "🚀 Sprawdź status logowania"
            });

            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "AUTH-002", 
                Name = "Test API WhoAmI - informacje o użytkowniku",
                Description = "Sprawdza czy lokalny API TeamsManager poprawnie zwraca informacje o aktualnie zalogowanym użytkowniku. " +
                             "Test weryfikuje działanie endpointu /api/TestAuth/whoami oraz poprawność parsowania tokenu JWT.",
                Category = "Uwierzytelnianie",
                Priority = "Wysoki",
                ExpectedResult = "API zwraca JSON z informacjami o użytkowniku: UPN, ObjectId, typ uwierzytelniania i listę claims",
                Steps = new List<string>
                {
                    "Upewnij się że jesteś zalogowany do aplikacji", 
                    "Kliknij przycisk 'Sprawdź informacje o użytkowniku'",
                    "Sprawdź czy API zwraca status 200 OK",
                    "Zweryfikuj czy zwrócone dane zawierają poprawny UserPrincipalName",
                    "Sprawdź czy ObjectId jest obecny i ma format UUID",
                    "Zweryfikuj czy AuthenticationType to 'Bearer'"
                },
                HasAutomaticExecution = true,
                AutoExecuteButtonText = "🔍 Wykonaj test WhoAmI"
            });
        }

        private void AddGraphApiTests()
        {
            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "GRAPH-001",
                Name = "Test dostępu do Microsoft Graph API",
                Description = "Weryfikuje czy aplikacja może uzyskać dedykowany token dla Microsoft Graph API i uzyskać dostęp " +
                             "do podstawowych informacji użytkownika. Test sprawdza działanie endpointów /me i /me/photo.",
                Category = "API Graph",
                Priority = "Średni",
                ExpectedResult = "Aplikacja pomyślnie łączy się z Graph API, pobiera profil użytkownika i jego zdjęcie",
                Steps = new List<string>
                {
                    "Upewnij się że jesteś zalogowany",
                    "Kliknij przycisk testowania Graph API",
                    "Sprawdź czy aplikacja uzyskuje token Graph (może wymagać dodatkowych zgód)",
                    "Zweryfikuj czy endpoint /me zwraca dane profilu",
                    "Sprawdź czy endpoint /me/photo zwraca zdjęcie użytkownika lub błąd 404 (brak zdjęcia to OK)",
                    "Sprawdź wyniki w polu tekstowym"
                },
                HasAutomaticExecution = true,
                AutoExecuteButtonText = "🌐 Test Graph API"
            });

            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "GRAPH-002",
                Name = "Test ładowania profilu użytkownika na ekranie głównym",
                Description = "Sprawdza czy główny ekran aplikacji poprawnie ładuje i wyświetla profil użytkownika z Graph API, " +
                             "w tym jego nazwę wyświetlaną i awatar.",
                Category = "API Graph", 
                Priority = "Średni",
                ExpectedResult = "Na ekranie głównym widoczna jest nazwa użytkownika i jego awatar zamiast fragmentu tokenu",
                Steps = new List<string>
                {
                    "Zaloguj się do aplikacji",
                    "Sprawdź lewy górny róg głównego okna",
                    "Zweryfikuj czy wyświetlana jest pełna nazwa użytkownika (DisplayName)",
                    "Sprawdź czy awatar użytkownika jest wyświetlany poprawnie",
                    "Jeśli awatar nie jest dostępny, powinien być wyświetlony domyślny placeholder",
                    "Upewnij się że nie są widoczne fragmenty tokenów"
                },
                HasAutomaticExecution = false
            });
        }

        private void AddPowerShellTests()
        {
            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "PS-001",
                Name = "Test połączenia PowerShell z Microsoft Graph",
                Description = "Weryfikuje czy serwis PowerShell może pomyślnie nawiązać połączenie z Microsoft Graph przy użyciu " +
                             "tokenu uzyskanego przez przepływ On-Behalf-Of. Test sprawdza czy moduł Microsoft.Graph jest dostępny " +
                             "i czy można wykonać polecenia Graph przez PowerShell.",
                Category = "PowerShell",
                Priority = "Krytyczny",
                ExpectedResult = "PowerShell pomyślnie łączy się z Graph, zwraca status połączenia i podstawowe informacje",
                Steps = new List<string>
                {
                    "Upewnij się że jesteś zalogowany do aplikacji",
                    "Kliknij przycisk testowania PowerShell",
                    "Sprawdź czy API wykonuje przepływ On-Behalf-Of",
                    "Zweryfikuj czy PowerShell Service otrzymuje token Graph",
                    "Sprawdź czy polecenie Connect-MgGraph wykonuje się pomyślnie",
                    "Sprawdź status połączenia w wynikach testu"
                },
                HasAutomaticExecution = true,
                AutoExecuteButtonText = "🔧 Test PowerShell Graph"
            });

            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "PS-002",
                Name = "Test poleceń PowerShell Microsoft.Graph",
                Description = "Sprawdza czy po nawiązaniu połączenia można wykonać podstawowe polecenia modułu Microsoft.Graph " +
                             "jak Get-MgUser, Get-MgGroup itp.",
                Category = "PowerShell",
                Priority = "Wysoki", 
                ExpectedResult = "Polecenia PowerShell Graph wykonują się pomyślnie i zwracają dane",
                Steps = new List<string>
                {
                    "Nawiąż połączenie PowerShell z Graph (test PS-001)",
                    "Wykonaj polecenie Get-MgContext sprawdzające kontekst",
                    "Wykonaj Get-MgUser -UserId 'me' pobierające profil",
                    "Sprawdź czy polecenia zwracają dane bez błędów",
                    "Zweryfikuj czy dane są w poprawnym formacie JSON/PowerShell"
                },
                HasAutomaticExecution = false
            });
        }

        private void AddTeamsManagementTests()
        {
            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "TEAMS-001",
                Name = "Test pobierania listy zespołów użytkownika",
                Description = "Weryfikuje czy aplikacja może pobrać listę zespołów Microsoft Teams do których należy użytkownik. " +
                             "Test sprawdza działanie API endpoint i poprawność zwracanych danych.",
                Category = "Zarządzanie Teams",
                Priority = "Wysoki",
                ExpectedResult = "API zwraca listę zespołów z podstawowymi informacjami: nazwa, opis, liczba członków",
                Steps = new List<string>
                {
                    "Zaloguj się do aplikacji", 
                    "Przejdź do sekcji zarządzania zespołami",
                    "Kliknij 'Pobierz zespoły'",
                    "Sprawdź czy lista zespołów się ładuje",
                    "Zweryfikuj czy każdy zespół ma wyświetlaną nazwę i podstawowe informacje",
                    "Sprawdź czy można rozwinąć szczegóły zespołu"
                },
                HasAutomaticExecution = false
            });

            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "TEAMS-002",
                Name = "Test zarządzania członkami zespołu",
                Description = "Sprawdza czy aplikacja pozwala na przeglądanie i zarządzanie członkami wybranego zespołu Teams.",
                Category = "Zarządzanie Teams",
                Priority = "Średni",
                ExpectedResult = "Możliwość przeglądania listy członków, dodawania i usuwania użytkowników z zespołu",
                Steps = new List<string>
                {
                    "Wybierz zespół z listy",
                    "Kliknij 'Zarządzaj członkami'",
                    "Sprawdź czy lista członków się ładuje",
                    "Sprawdź czy można wyszukać nowych użytkowników",
                    "Przetestuj dodawanie nowego członka (jeśli masz uprawnienia)",
                    "Sprawdź czy zmiany są zapisywane"
                },
                HasAutomaticExecution = false
            });
        }

        private void AddUITests()
        {
            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "UI-001",
                Name = "Test responsywności interfejsu użytkownika",
                Description = "Weryfikuje czy interfejs aplikacji jest responsywny, elementy ładują się szybko " +
                             "i aplikacja reaguje na działania użytkownika bez opóźnień.",
                Category = "Interfejs",
                Priority = "Średni",
                ExpectedResult = "Interfejs jest płynny, przyciski reagują natychmiast, okna ładują się w rozsądnym czasie",
                Steps = new List<string>
                {
                    "Otwórz główne okno aplikacji",
                    "Sprawdź czas ładowania elementów interfejsu",
                    "Kliknij różne przyciski i sprawdź responsywność",
                    "Otwórz okno testów i sprawdź czy ładuje się płynnie",
                    "Przełączaj między kategoriami testów",
                    "Sprawdź czy scrollowanie działa płynnie"
                },
                HasAutomaticExecution = false
            });

            _currentTestSuite.TestCases.Add(new TestCase
            {
                Id = "UI-002",
                Name = "Test motywu ciemnego Windows 11",
                Description = "Sprawdza czy aplikacja poprawnie stosuje ciemny motyw zgodny z Windows 11 " +
                             "i czy wszystkie elementy są czytelne i estetyczne.",
                Category = "Interfejs",
                Priority = "Niski",
                ExpectedResult = "Aplikacja ma spójny ciemny motyw, tekst jest czytelny, kolory są zgodne z Windows 11",
                Steps = new List<string>
                {
                    "Sprawdź czy okna mają ciemne tło",
                    "Zweryfikuj czy tekst jest wyraźnie widoczny",
                    "Sprawdź czy przyciski mają odpowiednie efekty hover",
                    "Sprawdź czy paski przewijania są stylizowane",
                    "Zweryfikuj spójność kolorów w całej aplikacji",
                    "Sprawdź czy ikony są czytelne na ciemnym tle"
                },
                HasAutomaticExecution = false
            });
        }

        private void LoadTestsToCategories()
        {
            // Czyść listy kategorii
            AuthTestsList.Items.Clear();
            GraphApiTestsList.Items.Clear(); 
            PowerShellTestsList.Items.Clear();
            TeamsManagementTestsList.Items.Clear();
            UiTestsList.Items.Clear();

            // Załaduj testy do odpowiednich kategorii
            foreach (var test in _currentTestSuite.TestCases)
            {
                var testViewModel = new TestCaseViewModel
                {
                    TestCase = test,
                    ResultIcon = GetResultIcon(test.Result)
                };

                switch (test.Category)
                {
                    case "Uwierzytelnianie":
                        AuthTestsList.Items.Add(testViewModel);
                        break;
                    case "API Graph":
                        GraphApiTestsList.Items.Add(testViewModel);
                        break;
                    case "PowerShell":
                        PowerShellTestsList.Items.Add(testViewModel);
                        break;
                    case "Zarządzanie Teams":
                        TeamsManagementTestsList.Items.Add(testViewModel);
                        break;
                    case "Interfejs":
                        UiTestsList.Items.Add(testViewModel);
                        break;
                }
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdź stan wszystkich expanderów
            var allExpanders = new List<Expander> 
            { 
                AuthCategoryExpander, 
                GraphApiCategoryExpander, 
                PowerShellCategoryExpander, 
                TeamsManagementCategoryExpander, 
                UiCategoryExpander 
            };
            
            // Jeśli przynajmniej jeden jest rozwinięty, zwiń wszystkie
            // Jeśli wszystkie są zwinięte, rozwiń wszystkie
            bool anyExpanded = allExpanders.Any(exp => exp.IsExpanded);
            
            if (anyExpanded)
            {
                // Zwiń wszystkie z animacją
                foreach (var expander in allExpanders)
                {
                    if (expander.IsExpanded)
                    {
                        var collapseAnimation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
                        collapseAnimation.Completed += (s, args) => expander.IsExpanded = false;
                        expander.BeginAnimation(OpacityProperty, collapseAnimation);
                    }
                }
            }
            else
            {
                // Rozwiń wszystkie z animacją kaskadową
                double delay = 0;
                foreach (var expander in allExpanders)
                {
                    expander.IsExpanded = true;
                    
                    var expandAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3))
                    {
                        BeginTime = TimeSpan.FromSeconds(delay)
                    };
                    expander.BeginAnimation(OpacityProperty, expandAnimation);
                    
                    delay += 0.05; // Kaskadowy efekt
                }
            }
            
            // Animacja obrotu ikony hamburgera
            if (sender is Button button && button.Content is PackIcon icon)
            {
                var rotateAnimation = new DoubleAnimation
                {
                    From = anyExpanded ? 0 : 180,
                    To = anyExpanded ? 180 : 360,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                
                var rotateTransform = new RotateTransform();
                icon.RenderTransform = rotateTransform;
                icon.RenderTransformOrigin = new Point(0.5, 0.5);
                
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
            }
        }

        private void TestCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is TestCaseViewModel selectedViewModel)
            {
                // Odznacz inne listy
                var allLists = new List<ListBox> 
                { 
                    AuthTestsList, 
                    GraphApiTestsList, 
                    PowerShellTestsList, 
                    TeamsManagementTestsList, 
                    UiTestsList 
                };
                
                foreach (var list in allLists.Where(l => l != listBox))
                {
                    list.SelectedItem = null;
                }

                _selectedTestCase = selectedViewModel.TestCase;
                
                // Animacja fade przy zmianie testu
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.1));
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
                
                TestTitle.BeginAnimation(OpacityProperty, fadeOut);
                fadeOut.Completed += (s, args) =>
                {
                    DisplayTestDetails(_selectedTestCase);
                    TestTitle.BeginAnimation(OpacityProperty, fadeIn);
                };
            }
        }

        private void RefreshTestList()
        {
            LoadTestsToCategories();
        }

        private void DisplayTestDetails(TestCase testCase)
        {
            TestTitle.Text = testCase.Name;
            TestDescription.Text = testCase.Description;
            ExpectedResultText.Text = testCase.ExpectedResult;

            // Wyświetl endpoint API jeśli dostępny
            if (!string.IsNullOrEmpty(testCase.ApiEndpoint))
            {
                ApiEndpointPanel.Visibility = Visibility.Visible;
                ApiEndpointText.Text = testCase.ApiEndpoint;
            }
            else
            {
                ApiEndpointPanel.Visibility = Visibility.Collapsed;
            }

            // Załaduj kroki
            var steps = new List<TestStepViewModel>();
            for (int i = 0; i < testCase.Steps.Count; i++)
            {
                steps.Add(new TestStepViewModel
                {
                    StepNumber = $"{i + 1}",
                    StepText = testCase.Steps[i]
                });
            }
            TestStepsList.ItemsSource = steps;

            // Wyczyść poprzednie wyniki jeśli test się zmienił
            if (TestResultTextBox.Text.StartsWith("Tu pojawią się wyniki"))
            {
                TestResultTextBox.Text = $"Wybrano test: {testCase.Id}\nGotowy do wykonania...";
            }

            // Dodaj przycisk automatycznego wykonania jeśli dostępny
            AddAutomaticTestButton(testCase);
        }

        private void AddAutomaticTestButton(TestCase testCase)
        {
            // Usuń poprzedni przycisk automatyczny jeśli istniał
            var existingButton = TestStepsList.Parent as StackPanel;
            var parent = existingButton?.Parent as StackPanel;
            
            // Znajdź kontener kroków
            var stepsContainer = TestStepsList.Parent as StackPanel;
            if (stepsContainer == null) return;

            // Usuń poprzedni przycisk automatyczny
            var existingAutoButton = stepsContainer.Children.OfType<Button>()
                .FirstOrDefault(b => b.Name == "AutoExecuteButton");
            if (existingAutoButton != null)
            {
                stepsContainer.Children.Remove(existingAutoButton);
            }

            // Dodaj przycisk automatycznego wykonania jeśli test go obsługuje
            if (testCase.HasAutomaticExecution && !string.IsNullOrEmpty(testCase.AutoExecuteButtonText))
            {
                var autoButton = new Button
                {
                    Name = "AutoExecuteButton",
                    Content = testCase.AutoExecuteButtonText,
                    Margin = new Thickness(0, 15, 0, 0),
                    Style = (Style)FindResource("PrimaryActionButton")
                };

                autoButton.Click += (s, e) => ExecuteAutomaticTest(testCase);
                stepsContainer.Children.Add(autoButton);
            }
        }

        private void ShowLoading(bool show, string message = "Wykonywanie testu...")
        {
            if (show)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                
                // Znajdź TextBlock w LoadingOverlay
                var textBlock = LoadingOverlay.Descendants<TextBlock>().FirstOrDefault();
                if (textBlock != null)
                {
                    textBlock.Text = message;
                }
                
                var fadeIn = new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(0.2));
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(0.2));
                fadeOut.Completed += (s, e) => LoadingOverlay.Visibility = Visibility.Collapsed;
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private async void ExecuteAutomaticTest(TestCase testCase)
        {
            ShowLoading(true, $"Wykonywanie testu {testCase.Id}...");
            
            var startTime = DateTime.Now;
            TestResultTextBox.Text = $"🔄 Wykonywanie testu {testCase.Id}...\n";
            ExecutionStatus.Text = "🔄 Test w trakcie wykonania...";

            try
            {
                bool success = false;
                string message = "";

                switch (testCase.Id)
                {
                    case "AUTH-001":
                        // Test statusu logowania
                        if (_authResult != null)
                        {
                            success = true;
                            message = $"✅ Użytkownik jest zalogowany\n" +
                                     $"UPN: {_authResult.Account?.Username}\n" +
                                     $"Tenant: {_authResult.TenantId}\n" +
                                     $"Token wygasa: {_authResult.ExpiresOn:yyyy-MM-dd HH:mm:ss}";
                        }
                        else
                        {
                            success = false;
                            message = "❌ Użytkownik nie jest zalogowany";
                        }
                        break;

                    case "AUTH-002":
                        // Test WhoAmI API
                        var whoAmIResult = await ExecuteWhoAmITest();
                        success = whoAmIResult.Success;
                        message = whoAmIResult.Message;
                        break;

                    case "GRAPH-001":
                        // Test Graph API
                        await ExecuteGraphApiTest();
                        return; // ExecuteGraphApiTest ma własną obsługę wyników

                    case "PS-001":
                        // Test PowerShell
                        var psResult = await ExecutePowerShellTest();
                        success = psResult.Success;
                        message = psResult.Message;
                        break;

                    default:
                        message = "❓ Ten test nie ma zaimplementowanej automatycznej wykonania";
                        break;
                }

                // Wyświetl wyniki
                TestResultTextBox.Text = $"📊 Wynik testu {testCase.Id}:\n\n{message}";
                ExecutionStatus.Text = success ? "✅ Test zakończony pomyślnie" : "❌ Test zakończony niepowodzeniem";

                // Zapisz do sesji
                var duration = DateTime.Now - startTime;
                await SaveTestSessionToMarkdown(testCase, startTime, duration, message);
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                string errorMsg = $"❌ Błąd podczas wykonywania testu: {ex.Message}";
                TestResultTextBox.Text = errorMsg;
                ExecutionStatus.Text = "❌ Błąd wykonania testu";
                
                await SaveTestSessionToMarkdown(testCase, startTime, duration, errorMsg);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task ExecuteGraphApiTest()
        {
            if (_authResult == null)
            {
                TestResultTextBox.Text = "❌ Musisz być zalogowany, aby wykonać test Graph API";
                ExecutionStatus.Text = "❌ Brak uwierzytelniania";
                return;
            }

            if (_msalAuthService == null)
            {
                TestResultTextBox.Text = "❌ Serwis MSAL nie jest dostępny";
                ExecutionStatus.Text = "❌ Błąd konfiguracji";
                return;
            }

            try
            {
                TestResultTextBox.Text = "🔄 Testowanie dostępu do Microsoft Graph API...\n";
                ExecutionStatus.Text = "🔄 Pobieranie tokenu Graph...";

                // Pobierz dedykowany token Graph API
                string? graphToken = await _msalAuthService.AcquireGraphTokenAsync();
                
                if (string.IsNullOrEmpty(graphToken))
                {
                    TestResultTextBox.Text += "🔄 Próba pobrania tokenu interaktywnie...\n";
                    graphToken = await _msalAuthService.AcquireGraphTokenInteractiveAsync(this);
                }
                
                if (string.IsNullOrEmpty(graphToken))
                {
                    TestResultTextBox.Text = "❌ Nie udało się pobrać tokenu Graph API\n" +
                                           "Może być wymagana zgoda administratora lub dodatkowe uprawnienia.";
                    ExecutionStatus.Text = "❌ Brak dostępu do Graph API";
                    return;
                }

                TestResultTextBox.Text += "✅ Token Graph API pobrany pomyślnie\n";
                ExecutionStatus.Text = "🔄 Testowanie endpointów Graph...";

                // Pobierz serwis Graph z DI
                var graphService = App.ServiceProvider.GetRequiredService<TeamsManager.UI.Services.Abstractions.IGraphUserProfileService>();
                var testResult = await graphService.TestGraphAccessAsync(graphToken);

                // Wyświetl szczegółowe wyniki
                var results = new StringBuilder();
                results.AppendLine("🔍 Wyniki testowania Microsoft Graph API:");
                results.AppendLine();
                results.AppendLine($"📋 Endpoint /me: {testResult.MeEndpointStatus}");
                results.AppendLine($"   Dostęp do profilu: {(testResult.CanAccessProfile ? "✅ TAK" : "❌ NIE")}");
                results.AppendLine();
                results.AppendLine($"📸 Endpoint /me/photo: {testResult.PhotoEndpointStatus}");
                results.AppendLine($"   Dostęp do zdjęć: {(testResult.CanAccessPhoto ? "✅ TAK" : "❌ NIE")}");
                
                if (!string.IsNullOrEmpty(testResult.ErrorMessage))
                {
                    results.AppendLine();
                    results.AppendLine($"⚠️ Dodatkowe informacje:");
                    results.AppendLine($"   {testResult.ErrorMessage}");
                }

                results.AppendLine();
                results.AppendLine("📈 Podsumowanie:");
                if (testResult.CanAccessProfile)
                {
                    results.AppendLine("✅ Test Graph API zakończony pomyślnie - aplikacja ma dostęp do podstawowych danych profilu");
                }
                else
                {
                    results.AppendLine("❌ Test Graph API nie powiódł się - brak dostępu do profilu użytkownika");
                }

                TestResultTextBox.Text = results.ToString();
                ExecutionStatus.Text = testResult.CanAccessProfile ? "✅ Test Graph API - sukces" : "❌ Test Graph API - niepowodzenie";
            }
            catch (Exception ex)
            {
                TestResultTextBox.Text = $"❌ Błąd podczas testowania Graph API:\n{ex.Message}";
                ExecutionStatus.Text = "❌ Błąd testu Graph API";
                System.Diagnostics.Debug.WriteLine($"[ManualTestingWindow] Graph API test error: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> ExecuteWhoAmITest()
        {
            if (_authResult == null || string.IsNullOrEmpty(_authResult.AccessToken))
            {
                return (false, "Brak tokenu dostępu");
            }

            if (_httpClient == null)
            {
                return (false, "HttpClient nie został zainicjalizowany");
            }

            string apiUrl = "https://localhost:7037/api/TestAuth/whoami";

            try
            {
                // Ustawienie nagłówka autoryzacji - pozwalamy na przełączanie tokenów
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);

                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("WhoAmI test succeeded with status {StatusCode}", response.StatusCode);
                    return (true, $"Sukces - Status: {response.StatusCode}, Odpowiedź: {responseBody}");
                }
                else
                {
                    _logger.LogWarning("WhoAmI test failed with status {StatusCode}", response.StatusCode);
                    return (false, $"Błąd API - Status: {response.StatusCode}, Odpowiedź: {responseBody}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request error in WhoAmI test");
                return (false, $"Błąd połączenia: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in WhoAmI test");
                return (false, $"Wyjątek: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> ExecutePowerShellTest()
        {
            if (_authResult == null || string.IsNullOrEmpty(_authResult.AccessToken))
            {
                return (false, "Brak tokenu dostępu");
            }

            if (_httpClient == null)
            {
                return (false, "HttpClient nie został zainicjalizowany");
            }

            string apiUrl = "https://localhost:7037/api/PowerShell/test-connection";

            try
            {
                // Ustawienie nagłówka autoryzacji
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);

                HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, null);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("PowerShell test succeeded with status {StatusCode}", response.StatusCode);
                    return (true, $"Sukces - Status: {response.StatusCode}, Odpowiedź: {responseBody}");
                }
                else
                {
                    _logger.LogWarning("PowerShell test failed with status {StatusCode}", response.StatusCode);
                    return (false, $"Błąd API - Status: {response.StatusCode}, Odpowiedź: {responseBody}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request error in PowerShell test");
                return (false, $"Błąd połączenia: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PowerShell test");
                return (false, $"Wyjątek: {ex.Message}");
            }
        }

        private async Task SaveTestSessionToMarkdown(TestCase testCase, DateTime executionTime, TimeSpan duration, string errorDetails)
        {
            string markdownFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SesjeTestowe.md");
            
            var sessionEntry = new StringBuilder();
            
            // Sprawdź czy plik istnieje, jeśli nie - dodaj nagłówek
            bool fileExists = File.Exists(markdownFile);
            if (!fileExists)
            {
                sessionEntry.AppendLine("# Sesje Testowe - TeamsManager");
                sessionEntry.AppendLine();
            }

            // Sprawdź czy dla tej sesji już istnieje tabela
            string sessionHeader = $"## Sesja testowa - {_sessionStartTime:yyyy-MM-dd HH:mm:ss}";
            string tableHeader = "| Czas wykonania | Nazwa testu | Wynik | Komunikat | Czas trwania | Użytkownik |";
            string tableSeparator = "|---|---|---|---|---|---|";

            string existingContent = fileExists ? await File.ReadAllTextAsync(markdownFile) : "";
            
            if (!existingContent.Contains(sessionHeader))
            {
                // Nowa sesja - dodaj nagłówek i tabelę
                sessionEntry.AppendLine(sessionHeader);
                sessionEntry.AppendLine();
                sessionEntry.AppendLine(tableHeader);
                sessionEntry.AppendLine(tableSeparator);
            }

            // Dodaj wpis o teście
            string resultIcon = GetResultIcon(testCase.Result);
            string userName = _authResult?.Account?.Username ?? "Niezalogowany";
            string message = !string.IsNullOrEmpty(errorDetails) ? errorDetails.Replace("|", "\\|") : "Wykonano poprawnie";
            string durationStr = duration.TotalSeconds > 0 ? $"{duration.TotalSeconds:F2}s" : "N/A";

            var testEntry = $"| {executionTime:HH:mm:ss} | {testCase.Name} | {resultIcon} {testCase.Result} | {message} | {durationStr} | {userName} |";
            
            if (existingContent.Contains(sessionHeader))
            {
                // Dodaj do istniejącej tabeli
                await File.AppendAllTextAsync(markdownFile, testEntry + Environment.NewLine);
            }
            else
            {
                // Nowa sesja
                sessionEntry.AppendLine(testEntry);
                sessionEntry.AppendLine();
                await File.AppendAllTextAsync(markdownFile, sessionEntry.ToString());
            }
        }

        private string GetResultIcon(TestResult result)
        {
            return result switch
            {
                TestResult.Pass => "✅",
                TestResult.Fail => "❌",
                TestResult.Warning => "⚠️",
                TestResult.Skip => "⏭️",
                _ => "⏸️"
            };
        }

        private void UpdateStatistics()
        {
            var total = _currentTestSuite.TotalTests;
            var passed = _currentTestSuite.PassedTests;
            var failed = _currentTestSuite.FailedTests;
            var warnings = _currentTestSuite.WarningTests;
            var skipped = _currentTestSuite.SkippedTests;

            StatsText.Text = $"Łącznie: {total} | ✅ {passed} | ❌ {failed} | ⚠️ {warnings} | ⏭️ {skipped}";

            var completedTests = passed + failed + warnings + skipped;
            var progressPercentage = total > 0 ? (double)completedTests / total * 100 : 0;
            ProgressBar.Value = progressPercentage;
        }

        private async void LoadTests_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savedSuite = await _testingService.LoadTestResults();
                if (savedSuite != null)
                {
                    _currentTestSuite = savedSuite;
                    RefreshTestList();
                    UpdateStatistics();
                    MessageBox.Show("Wyniki testów zostały załadowane", "Sukces", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Nie znaleziono zapisanych wyników testów", "Informacja", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _testingService.SaveTestConfig(_currentTestSuite);
                MessageBox.Show("Wyniki testów zostały zapisane", "Sukces", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _testingService.SaveTestResults(_currentTestSuite);
                
                var filePath = _testingService.GetResultsFilePath();
                MessageBox.Show($"Raport wygenerowany:\n{filePath}", "Sukces", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas generowania raportu: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = _testingService.GetResultsFilePath();
                if (System.IO.File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Raport nie został jeszcze wygenerowany. Użyj 'Generuj Raport' najpierw.", 
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania raportu: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackToMain_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Znajdź główne okno (MainShellWindow) i przenieś na pierwszy plan
                var mainWindow = System.Windows.Application.Current.Windows
                    .OfType<Views.Shell.MainShellWindow>()
                    .FirstOrDefault();
                    
                if (mainWindow != null)
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                    mainWindow.Focus();
                }
                
                // Minimalizuj okno testów
                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przejścia do głównego okna: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task SaveLoginResultToSession(bool success, string message, TimeSpan duration)
        {
            var executionTime = DateTime.Now;
            
            // Znajdź test logowania
            var loginTest = _currentTestSuite.TestCases.FirstOrDefault(t => t.Id == "AUTH-001");
            if (loginTest != null)
            {
                loginTest.Result = success ? TestResult.Pass : TestResult.Fail;
                loginTest.ExecutedAt = executionTime;
                loginTest.ErrorDetails = message;

                await SaveTestSessionToMarkdown(loginTest, executionTime, duration, message);
                await _testingService.SaveTestConfig(_currentTestSuite);

                RefreshTestList();
                UpdateStatistics();
            }
        }

        private async Task GenerateSessionReportOnCloseAsync()
        {
            try
            {
                var executedTests = _currentTestSuite.TestCases.Where(t => t.ExecutedAt.HasValue).ToList();
                
                if (executedTests.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[ManualTestingWindow] Generowanie raportu sesji - wykonano {executedTests.Count} testów");
                    
                    // Zapisz końcowy raport sesji do markdown
                    string markdownFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SesjeTestowe.md");
                    var sessionSummary = new StringBuilder();
                    
                    var sessionEndTime = DateTime.Now;
                    var sessionDuration = sessionEndTime - _sessionStartTime;
                    
                    sessionSummary.AppendLine();
                    sessionSummary.AppendLine($"### Podsumowanie sesji zakończonej o {sessionEndTime:yyyy-MM-dd HH:mm:ss}");
                    sessionSummary.AppendLine($"**Czas trwania sesji:** {sessionDuration.TotalMinutes:F1} minut");
                    sessionSummary.AppendLine($"**Wykonanych testów:** {executedTests.Count}");
                    sessionSummary.AppendLine($"**Pomyślnych:** {executedTests.Count(t => t.Result == TestResult.Pass)}");
                    sessionSummary.AppendLine($"**Nieudanych:** {executedTests.Count(t => t.Result == TestResult.Fail)}");
                    sessionSummary.AppendLine($"**Ostrzeżeń:** {executedTests.Count(t => t.Result == TestResult.Warning)}");
                    sessionSummary.AppendLine($"**Pominięte:** {executedTests.Count(t => t.Result == TestResult.Skip)}");
                    sessionSummary.AppendLine();

                    await File.AppendAllTextAsync(markdownFile, sessionSummary.ToString());
                    
                    // Zapisz również wyniki do systemu
                    await _testingService.SaveTestResults(_currentTestSuite);
                    
                    System.Diagnostics.Debug.WriteLine($"[ManualTestingWindow] Raport sesji zapisany do: {markdownFile}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ManualTestingWindow] Błąd podczas generowania raportu sesji: {ex}");
            }
        }

        private async void MarkResult_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTestCase == null)
            {
                MessageBox.Show("Wybierz test z listy", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (sender is Button button && Enum.TryParse<TestResult>(button.Tag.ToString(), out var result))
            {
                var executionTime = DateTime.Now;
                var duration = TimeSpan.Zero;
                string errorMessage = NotesTextBox.Text.Trim();

                _selectedTestCase.Result = result;
                _selectedTestCase.ExecutedAt = executionTime;
                _selectedTestCase.Notes = NotesTextBox.Text;
                _selectedTestCase.ErrorDetails = errorMessage;

                // Aktualizuj widok
                RefreshTestList();
                UpdateStatistics();
                DisplayTestDetails(_selectedTestCase);

                // Zapisz wynik do sesji markdown
                try
                {
                    await SaveTestSessionToMarkdown(_selectedTestCase, executionTime, duration, errorMessage);
                    await _testingService.SaveTestConfig(_currentTestSuite);
                    
                    ExecutionStatus.Text = $"✅ Wynik zapisany: {result} - {executionTime:HH:mm:ss}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // ViewModel classes dla binding
    public class TestCaseViewModel
    {
        public TestCase TestCase { get; set; } = null!;
        public string ResultIcon { get; set; } = "⏸️";
        public string Id => TestCase.Id;
        public string Name => TestCase.Name;
        public string Priority => TestCase.Priority;
        public List<TestStepViewModel> StepNumber { get; set; } = new();
    }

    public class TestStepViewModel
    {
        public string StepNumber { get; set; } = string.Empty;
        public string StepText { get; set; } = string.Empty;
    }
}

// Extension method helper
public static class VisualTreeExtensions
{
    public static IEnumerable<T> Descendants<T>(this DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(parent);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var childrenCount = VisualTreeHelper.GetChildrenCount(current);
            
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is T typedChild)
                    yield return typedChild;
                queue.Enqueue(child);
            }
        }
    }
}
