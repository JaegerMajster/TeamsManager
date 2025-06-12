# Uniwersalny System Dialogów

## Przegląd

Nowy uniwersalny system dialogów zastępuje dotychczasowe MessageBox'y spójnym, nowoczesnym interfejsem użytkownika. System obsługuje różne typy komunikatów i oferuje elastyczną konfigurację.

## Komponenty Systemu

### 1. Modele Danych

#### `DialogType` (Enum)
- `Information` - Informacja (niebieska ikona)
- `Warning` - Ostrzeżenie (żółta ikona)  
- `Error` - Błąd (czerwona ikona)
- `Success` - Sukces (zielona ikona)
- `Confirmation` - Potwierdzenie (pytajnik, przyciski Tak/Nie)
- `Question` - Pytanie (pytajnik, niestandardowe przyciski)

#### `DialogResult` (Enum)
- `None` - Brak wyniku
- `OK` - Potwierdzenie
- `Cancel` - Anulowanie
- `Yes` - Tak
- `No` - Nie
- `Primary` - Główny przycisk
- `Secondary` - Drugi przycisk

#### `DialogOptions` (Klasa)
```csharp
public class DialogOptions
{
    public DialogType Type { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string? Details { get; set; }
    public string? PrimaryButtonText { get; set; }
    public string? SecondaryButtonText { get; set; }
    public bool ShowSecondaryButton { get; set; }
    public bool IsPrimaryDefault { get; set; }
    public bool IsSecondaryCancel { get; set; }
    public double? MaxWidth { get; set; }
    public bool AllowFormatting { get; set; }
}
```

#### `DialogResponse` (Klasa)
```csharp
public class DialogResponse
{
    public DialogResult Result { get; set; }
    public bool IsPrimary { get; }
    public bool IsSecondary { get; }
    public bool IsCancelled { get; }
    public TimeSpan DisplayTime { get; set; }
    public object? Tag { get; set; }
}
```

### 2. Interfejs Serwisu

#### `IUIDialogService` - Nowe Metody
```csharp
// Uniwersalny dialog
Task<DialogResponse> ShowDialogAsync(DialogOptions options);

// Podstawowe typy
Task<DialogResponse> ShowInformationAsync(string title, string message, string? details = null);
Task<DialogResponse> ShowWarningAsync(string title, string message, string? details = null);
Task<DialogResponse> ShowErrorAsync(string title, string message, string? details = null);
Task<DialogResponse> ShowSuccessAsync(string title, string message, string? details = null);

// Potwierdzenia
Task<DialogResponse> ShowConfirmationAsync(string title, string message, string? details = null, 
    string? yesText = null, string? noText = null);

// Niestandardowe
Task<DialogResponse> ShowQuestionAsync(string title, string message, string? details = null,
    string? primaryText = null, string? secondaryText = null);
```

### 3. Klasa Pomocnicza `DialogHelpers`

Zawiera gotowe metody dla typowych scenariuszy:

#### Błędy
- `ShowValidationErrorAsync()` - Błędy walidacji
- `ShowOperationErrorAsync()` - Błędy operacji
- `ShowConnectionErrorAsync()` - Błędy połączenia

#### Sukces
- `ShowOperationSuccessAsync()` - Sukces operacji
- `ShowSaveSuccessAsync()` - Sukces zapisu

#### Ostrzeżenia
- `ShowDataConflictWarningAsync()` - Konflikty danych
- `ShowConstraintWarningAsync()` - Ograniczenia systemu

#### Potwierdzenia
- `ShowDeleteConfirmationAsync()` - Potwierdzenie usunięcia
- `ShowDeactivateConfirmationAsync()` - Potwierdzenie deaktywacji
- `ShowSaveChangesConfirmationAsync()` - Zapisanie zmian
- `ShowCancelOperationConfirmationAsync()` - Anulowanie operacji

#### Pytania
- `ShowReplaceDataQuestionAsync()` - Zastąpienie danych
- `ShowContinueWithErrorsQuestionAsync()` - Kontynuacja przy błędach

#### Informacje
- `ShowOperationCompleteAsync()` - Zakończenie operacji
- `ShowNoDataInfoAsync()` - Brak danych

## Przykłady Użycia

### 1. Podstawowe Komunikaty

```csharp
// Informacja
await _dialogService.ShowInformationAsync(
    "Informacja", 
    "Operacja zakończona pomyślnie.");

// Ostrzeżenie
await _dialogService.ShowWarningAsync(
    "Ostrzeżenie", 
    "Nie można wykonać operacji.",
    "Szczegóły: Brak uprawnień do tego zasobu.");

// Błąd
await _dialogService.ShowErrorAsync(
    "Błąd", 
    "Wystąpił nieoczekiwany błąd.",
    "Exception: NullReferenceException at line 42");

// Sukces
await _dialogService.ShowSuccessAsync(
    "Sukces", 
    "Dział został utworzony pomyślnie.");
```

### 2. Potwierdzenia

```csharp
// Podstawowe potwierdzenie
var response = await _dialogService.ShowConfirmationAsync(
    "Potwierdzenie", 
    "Czy na pewno chcesz kontynuować?");

if (response.IsPrimary) // Użytkownik kliknął "Tak"
{
    // Wykonaj operację
}

// Potwierdzenie usunięcia (z pomocniczą metodą)
var deleteResponse = await DialogHelpers.ShowDeleteConfirmationAsync(
    _dialogService, 
    "Dział IT",
    "Dział zawiera 5 zespołów, które również zostaną usunięte.");

if (deleteResponse.IsPrimary)
{
    await DeleteDepartmentAsync();
}
```

### 3. Niestandardowe Dialogi

```csharp
// Dialog z niestandardowymi przyciskami
var response = await _dialogService.ShowQuestionAsync(
    "Konflikt danych", 
    "Znaleziono istniejące dane. Co chcesz zrobić?",
    "Istniejące dane: 150 rekordów\nNowe dane: 200 rekordów",
    "Zastąp wszystkie", 
    "Zachowaj istniejące");

if (response.IsPrimary)
{
    // Zastąp dane
}
else if (response.IsSecondary)
{
    // Zachowaj istniejące
}
```

### 4. Zaawansowana Konfiguracja

```csharp
var options = new DialogOptions
{
    Type = DialogType.Warning,
    Title = "Ostrzeżenie o wydajności",
    Message = "Operacja może potrwać długo.",
    Details = "Szacowany czas: 5-10 minut\nPrzetwarzanych rekordów: 10,000",
    ShowSecondaryButton = true,
    PrimaryButtonText = "Kontynuuj",
    SecondaryButtonText = "Anuluj",
    IsPrimaryDefault = false, // Nie zaznaczaj domyślnie "Kontynuuj"
    MaxWidth = 500
};

var response = await _dialogService.ShowDialogAsync(options);
```

## Migracja ze Starego Systemu

### Przed (MessageBox)
```csharp
var result = MessageBox.Show(
    "Czy na pewno chcesz usunąć?", 
    "Potwierdzenie", 
    MessageBoxButton.YesNo, 
    MessageBoxImage.Question);

if (result == MessageBoxResult.Yes)
{
    // Usuń
}
```

### Po (Nowy System)
```csharp
var response = await DialogHelpers.ShowDeleteConfirmationAsync(
    _dialogService, 
    "element");

if (response.IsPrimary)
{
    // Usuń
}
```

## Zalety Nowego Systemu

### 1. Spójność Wizualna
- Jednolity styl zgodny z aplikacją
- Material Design ikony i kolory
- Responsywny layout

### 2. Lepsze UX
- Czytelne komunikaty z opcjonalnymi szczegółami
- Intuicyjne przyciski z opisowymi tekstami
- Obsługa klawiatury (Enter, Escape)

### 3. Elastyczność
- Niestandardowe przyciski i teksty
- Różne typy dialogów
- Konfigurowalny wygląd

### 4. Łatwość Użycia
- Gotowe metody pomocnicze
- Async/await pattern
- Silnie typowane wyniki

### 5. Rozszerzalność
- Łatwe dodawanie nowych typów
- Możliwość dodania animacji
- Wsparcie dla formatowania tekstu

## Najlepsze Praktyki

### 1. Wybór Odpowiedniego Typu
- `Information` - Neutralne informacje
- `Success` - Potwierdzenie sukcesu operacji
- `Warning` - Ostrzeżenia wymagające uwagi
- `Error` - Błędy wymagające akcji użytkownika
- `Confirmation` - Potwierdzenia nieodwracalnych akcji

### 2. Teksty Komunikatów
- **Tytuł**: Krótki, opisowy (np. "Błąd walidacji")
- **Komunikat**: Jasny, zrozumiały dla użytkownika
- **Szczegóły**: Techniczne informacje (opcjonalne)

### 3. Przyciski
- Używaj opisowych tekstów ("Usuń" zamiast "OK")
- W potwierdzeniach nie zaznaczaj domyślnie destrukcyjnych akcji
- Ogranicz do maksymalnie 2 przycisków

### 4. Użycie Metod Pomocniczych
```csharp
// Dobrze - używa gotowej metody
await DialogHelpers.ShowValidationErrorAsync(_dialogService, 
    "Nazwa działu już istnieje.");

// Źle - ręczne tworzenie prostego dialogu
await _dialogService.ShowErrorAsync("Błąd walidacji", 
    "Nazwa działu już istnieje.");
```

## Kompatybilność Wsteczna

Stare metody są oznaczone jako `[Obsolete]` ale nadal działają:
- `ShowErrorDialog()` → `ShowErrorAsync()`
- `ShowInfoDialog()` → `ShowInformationAsync()`
- `ShowWarningDialog()` → `ShowWarningAsync()`
- `ShowSuccessDialog()` → `ShowSuccessAsync()`
- `ShowConfirmationDialog()` → `ShowConfirmationAsync()`

## Rejestracja w DI

System jest automatycznie zarejestrowany w `App.xaml.cs`:
```csharp
services.AddTransient<ViewModels.Dialogs.UniversalDialogViewModel>();
services.AddScoped<IUIDialogService, UIDialogService>();
``` 