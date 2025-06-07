# Architektura Dependency Injection w TeamsManager.UI

## 📋 Przegląd

TeamsManager.UI został w pełni zrefaktoryzowany do używania Microsoft.Extensions.DependencyInjection zgodnie z nowoczesnymi wzorcami .NET. Aplikacja implementuje kompleksowy system zarządzania zależnościami, który zapewnia:

- **Loosely Coupled Architecture** - komponenty komunikują się przez interfejsy
- **Testability** - łatwe mockowanie zależności w testach
- **Maintainability** - centralna konfiguracja serwisów
- **Scalability** - prostolinijne dodawanie nowych komponentów

## 🏗️ Komponenty DI

### Główne Serwisy

#### **IMsalAuthService** 
```csharp
services.AddSingleton<IMsalAuthService, MsalAuthService>();
```
- **Zakres:** Singleton (jeden instancja przez cały cykl życia aplikacji)
- **Odpowiedzialność:** Zarządzanie autentykacją Microsoft Identity Platform (MSAL)
- **Zależności:** `IMsalConfigurationProvider`, `ILogger<MsalAuthService>`

#### **IGraphUserProfileService**
```csharp
services.AddScoped<IGraphUserProfileService, GraphUserProfileService>();
```
- **Zakres:** Scoped (nowa instancja per request/operacja)
- **Odpowiedzialność:** Integracja z Microsoft Graph API dla profili użytkowników
- **Zależności:** `IHttpClientFactory`, `ILogger<GraphUserProfileService>`

#### **IManualTestingService**
```csharp
services.AddSingleton<IManualTestingService, ManualTestingService>();
```
- **Zakres:** Singleton (zachowuje stan testów między oknami)
- **Odpowiedzialność:** Zarządzanie testami manualnymi, zapis/odczyt wyników
- **Zależności:** Brak (samowystarczalny)

#### **IMsalConfigurationProvider**
```csharp
services.AddSingleton<IMsalConfigurationProvider, MsalConfigurationProvider>();
```
- **Zakres:** Singleton (konfiguracja ładowana raz)
- **Odpowiedzialność:** Dostarczanie konfiguracji MSAL z plików
- **Zależności:** `ILogger<MsalConfigurationProvider>`

### HTTP Pipeline

#### **HttpClientFactory**
```csharp
services.AddHttpClient();

// Named client dla Microsoft Graph
services.AddHttpClient("MicrosoftGraph", client => {...})
    .AddHttpMessageHandler<TokenAuthorizationHandler>()
    .AddStandardResilienceHandler();
```
- **TokenAuthorizationHandler** - automatyczne dodawanie tokenów Bearer
- **Resilience Patterns** - retry, circuit breaker, timeout
- **Typed Clients** - dedykowane konfiguracje dla różnych API

#### **TokenAuthorizationHandler**
```csharp
services.AddTransient<TokenAuthorizationHandler>();
```
- **Zakres:** Transient (nowa instancja per request)
- **Odpowiedzialność:** Automatyczne wstrzykiwanie tokenów do żądań HTTP

### Okna i UI

#### **MainWindow**
```csharp
services.AddTransient<MainWindow>();
```
- **Zakres:** Transient (choć w praktyce jeden per aplikacja)
- **Zależności:** `IMsalAuthService`, `IGraphUserProfileService`

#### **ManualTestingWindow**
```csharp
services.AddTransient<ManualTestingWindow>();
```
- **Zakres:** Transient (nowa instancja przy każdym otwarciu)
- **Zależności:** `IMsalAuthService`, `IManualTestingService`, `IHttpClientFactory`, `ILogger<ManualTestingWindow>`

## 🔧 Konfiguracja w App.xaml.cs

### Metoda ConfigureServices()

```csharp
private void ConfigureServices(IServiceCollection services)
{
    // Logging
    services.AddLogging(configure =>
    {
        configure.AddDebug();
        configure.SetMinimumLevel(LogLevel.Debug);
    });

    // Cache i podstawowe serwisy
    services.AddMemoryCache();
    services.AddSingleton<ICurrentUserService, CurrentUserService>();

    // Konfiguracja
    services.AddSingleton<ConfigurationManager>();
    services.AddSingleton<ConfigurationValidator>();
    services.AddSingleton<IMsalConfigurationProvider, MsalConfigurationProvider>();

    // Business Services
    services.AddSingleton<IMsalAuthService, MsalAuthService>();
    services.AddScoped<IGraphUserProfileService, GraphUserProfileService>();
    services.AddSingleton<IManualTestingService, ManualTestingService>();

    // HTTP Pipeline
    services.AddTransient<TokenAuthorizationHandler>();
    services.AddHttpClient("MicrosoftGraph", ...)
        .AddHttpMessageHandler<TokenAuthorizationHandler>()
        .AddStandardResilienceHandler();
    services.AddHttpClient(); // Default client

    // UI Components
    services.AddTransient<MainWindow>();
    services.AddTransient<ManualTestingWindow>();
    services.AddTransient<DashboardWindow>();
}
```

### OnStartup() - Rozwiązywanie Dependencies

```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    try
    {
        // Tworzenie głównego okna przez DI
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        
        // Weryfikacja DI
        var msalService = ServiceProvider.GetService<IMsalAuthService>();
        var graphService = ServiceProvider.GetService<IGraphUserProfileService>();
        // ... diagnostics
    }
    catch (Exception ex)
    {
        // Error handling
    }
}
```

## 🛠️ Wzorce użycia

### Konstruktor Injection

```csharp
public class ExampleWindow : Window
{
    private readonly IExampleService _service;
    private readonly ILogger<ExampleWindow> _logger;

    public ExampleWindow(
        IExampleService service,
        ILogger<ExampleWindow> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        InitializeComponent();
        InitializeServices();
    }

    private void InitializeServices()
    {
        try
        {
            // Użycie serwisu
            _logger.LogDebug("Inicjalizacja serwisów...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd inicjalizacji");
            // Graceful degradation
        }
    }
}
```

### Rejestracja w DI

```csharp
// W ConfigureServices()
services.AddTransient<ExampleWindow>();
services.AddScoped<IExampleService, ExampleService>();
```

### Tworzenie przez DI

```csharp
// W event handlerach
private void OpenExampleWindow_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var window = App.ServiceProvider.GetRequiredService<ExampleWindow>();
        window.Show();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd tworzenia okna");
        // Error handling
    }
}
```

## 🔍 Service Lifetimes

### Singleton
- **Kiedy:** Serwisy stanowe, konfiguracja, cache
- **Przykłady:** `IMsalAuthService`, `IManualTestingService`
- **Zalety:** Wydajność, zachowanie stanu
- **Uwagi:** Thread-safe implementation wymagane

### Scoped  
- **Kiedy:** Operacje per-request, transakcyjne
- **Przykłady:** `IGraphUserProfileService`
- **Zalety:** Izolacja między operacjami
- **Uwagi:** W WPF = per logical operation

### Transient
- **Kiedy:** Stateless serwisy, UI components
- **Przykłady:** `TokenAuthorizationHandler`, okna
- **Zalety:** Brak state conflicts
- **Uwagi:** Może być kosztowne dla heavy objects

## 🧪 Testowanie

### Unit Testing z DI

```csharp
[Test]
public void ExampleService_Should_Work()
{
    // Arrange
    var mockDependency = new Mock<IDependency>();
    var service = new ExampleService(mockDependency.Object);
    
    // Act & Assert
    // ...
}
```

### Integration Testing

```csharp
[Test]
public void DI_Container_Should_Resolve_All_Services()
{
    // Arrange
    var services = new ServiceCollection();
    
    // Act
    ConfigureTestServices(services);
    var provider = services.BuildServiceProvider();
    
    // Assert
    Assert.DoesNotThrow(() => provider.GetRequiredService<IMsalAuthService>());
    Assert.DoesNotThrow(() => provider.GetRequiredService<IGraphUserProfileService>());
}
```

## 📈 Performance Considerations

### Lazy Loading
```csharp
// Zamiast eager resolution w konstruktorze
private readonly Lazy<IExpensiveService> _expensiveService;

public ExampleClass(Lazy<IExpensiveService> expensiveService)
{
    _expensiveService = expensiveService;
}
```

### Factory Pattern
```csharp
// Dla wielu instancji tego samego typu
services.AddTransient<Func<string, INamedService>>(provider => name =>
    provider.GetServices<INamedService>().First(s => s.Name == name));
```

## 🔐 Security Considerations

### Token Management
- **Automatic:** `TokenAuthorizationHandler` dodaje tokeny automatycznie
- **Manual:** W `ManualTestingWindow` dla flexibilności przełączania tokenów
- **Storage:** Tokeny trzymane w memory, nie persistowane

### Configuration Security
- **Encryption:** `EncryptionService` dla wrażliwych danych
- **Validation:** `ConfigurationValidator` dla integralności
- **Isolation:** Każdy serwis ma access tylko do swoich dependencies

## 🚀 Migration Guide

### From Legacy to DI

**Przed:**
```csharp
public MainWindow()
{
    InitializeComponent();
    _msalService = new MsalAuthService();
    _httpClient = new HttpClient();
}
```

**Po:**
```csharp
public MainWindow(
    IMsalAuthService msalService,
    IHttpClientFactory httpClientFactory)
{
    InitializeComponent();
    _msalService = msalService ?? throw new ArgumentNullException(nameof(msalService));
    _httpClient = httpClientFactory.CreateClient();
}
```

### Dodawanie nowych serwisów

1. **Utwórz interfejs** w `Services/Abstractions/`
2. **Implementuj serwis** w `Services/`
3. **Zarejestruj w DI** w `App.xaml.cs`
4. **Inject gdzie potrzeba** przez konstruktor

## 📚 Best Practices

### ✅ Do
- Używaj interfejsów dla wszystkich dependencies
- Waliduj argumenty konstruktora (`?? throw new ArgumentNullException`)
- Loguj przez `ILogger<T>`
- Graceful degradation przy błędach DI
- Dokumentuj XML komentarzami

### ❌ Don't
- Nie używaj `new` dla serwisów (tylko dla models/DTOs)
- Nie rób Service Locator pattern (`App.ServiceProvider` tylko w root composition)
- Nie mieszaj scopes niepotrzebnie
- Nie trzymaj expensive objects jako Singleton bez powodu

## 🔧 Troubleshooting

### Częste problemy

**1. Circular Dependencies**
```
Error: A circular dependency was detected
```
**Rozwiązanie:** Użyj Lazy<T> lub przeprojektuj architekturę

**2. Multiple Constructors**
```
Error: Multiple constructors accepting all given argument types
```
**Rozwiązanie:** Jeden konstruktor per klasa dla DI

**3. Unregistered Service**
```
Error: Unable to resolve service for type 'IExampleService'
```
**Rozwiązanie:** Dodaj rejestrację w `ConfigureServices()`

### Debug DI Issues

```csharp
// W OnStartup() dla diagnostyk
var services = ServiceProvider.GetServices<IExampleService>();
_logger.LogDebug("Registered implementations: {Count}", services.Count());
```

---

## 📝 Podsumowanie

Architektura DI w TeamsManager.UI zapewnia:

- **Maintainable codebase** - łatwe zmiany i rozszerzenia
- **Testable components** - izolacja dependencies 
- **Performance optimization** - appropriate lifetimes
- **Error resilience** - graceful degradation patterns
- **Security** - controlled access to resources

Refaktoryzacja do DI przygotowuje aplikację na dalszy rozwój i ułatwia implementację nowych funkcjonalności zgodnie z nowoczesnymi wzorcami .NET. 