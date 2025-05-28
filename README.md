# TeamsManager - Dokumentacja projektu

## Informacje ogólne

TeamsManager to aplikacja lokalnie uruchamiana na komputerze, która działa jako zaawansowana nakładka na PowerShell do zarządzania grupami Microsoft Teams. Zamiast korzystać z licencjonowanego Microsoft Graph, aplikacja wykorzystuje darmowe skrypty PowerShell, oferując przy tym bogatą funkcjonalność i przyjazny interfejs użytkownika.

Projekt realizowany jest jako zaliczenie przedmiotu "Programowanie aplikacji sieciowych".

## 1. Architektura aplikacji

### Struktura rozwiązania

Rozwiązanie TeamsManager składa się z czterech głównych projektów, które razem tworzą kompletne rozwiązanie do zarządzania grupami Microsoft Teams.

#### TeamsManager.Core

Biblioteka zawierająca główną logikę biznesową aplikacji. Odpowiada za:
- Integrację z PowerShell i wykonywanie poleceń
- Zarządzanie grupami Teams (tworzenie, modyfikowanie, archiwizowanie)
- Operacje na użytkownikach (dodawanie, usuwanie)
- Definiowanie głównych interfejsów i modeli danych

Kluczowe klasy:
- `PowerShellService` - klasa do komunikacji z PowerShell
- `TeamsService` - klasa obsługująca operacje na zespołach Teams
- `UserService` - klasa obsługująca operacje na użytkownikach Teams

**Zaimplementowane klasy:**
- `PowerShellService` - podstawowa integracja z PowerShell i Microsoft Teams
  - Metody: ConnectToTeams(), CreateTeam(), Dispose()
  - Zarządzanie Runspace i obsługa błędów
  - Logowanie wszystkich operacji

#### TeamsManager.Api

Lokalny serwer REST API, który udostępnia funkcjonalność Core poprzez HTTP. Odpowiada za:
- Udostępnianie endpointów do zarządzania Teams
- Obsługę WebSockets do powiadomień w czasie rzeczywistym
- Komunikację z innymi instancjami aplikacji
- Uwierzytelnianie i autoryzację zapytań

Kluczowe komponenty:
- Kontrolery API (TeamsController, UsersController, TemplatesController)
- Hub SignalR dla powiadomień w czasie rzeczywistym
- Middleware do uwierzytelniania
- Serwisy hostujące długotrwałe operacje

#### TeamsManager.UI

Aplikacja WPF stanowiąca interfejs użytkownika. Odpowiada za:
- Prezentację danych i formularzy do zarządzania Teams
- Komunikację z API
- Odbieranie powiadomień w czasie rzeczywistym
- Prezentację postępu długotrwałych operacji

Kluczowe elementy:
- Główne okno aplikacji z nawigacją
- Widoki dla zarządzania zespołami, kanałami i użytkownikami
- Formularze do wprowadzania danych
- Komponenty do wyświetlania powiadomień i postępu

#### TeamsManager.Data

Biblioteka do zarządzania dostępem do danych. Odpowiada za:
- Przechowywanie szablonów zespołów
- Logowanie historii operacji
- Przechowywanie konfiguracji użytkownika
- Obsługę lokalnej bazy danych SQLite

Kluczowe elementy:
- DbContext dla Entity Framework Core
- Modele danych (TeamTemplate, OperationHistory)
- Repozytoria do obsługi operacji na danych
- Migracje bazy danych

### Diagramy i schematy

#### Diagram przepływu danych

```
+----------------+       +----------------+
|                |       |                |
|  TeamsManager  | <---> |  TeamsManager  |
|       UI       |       |      API       |
|                |       |                |
+----------------+       +----------------+
        ^                        ^
        |                        |
        v                        v
+----------------+       +----------------+
|                |       |                |
|  TeamsManager  | <---> |  PowerShell    |
|      Data      |       |   Modules      |
|                |       |                |
+----------------+       +----------------+
        ^
        |
        v
+----------------+
|                |
|     SQLite     |
|    Database    |
|                |
+----------------+
```

#### Diagram komunikacji między komponentami

```
+--------------------------------------------------+
|                                                  |
|                TeamsManager.UI                   |
|                                                  |
+--------------------------------------------------+
    |                   ^                   ^
    | HTTP Requests     | HTTP Responses    | WebSocket
    v                   |                   | Notifications
+--------------------------------------------------+
|                                                  |
|               TeamsManager.Api                   |
|                                                  |
+--------------------------------------------------+
    |                   ^                   ^
    | Service Calls     | Return Values     | Events
    v                   |                   |
+--------------------------------------------------+
|                                                  |
|               TeamsManager.Core                  |
|                                                  |
+--------------------------------------------------+
    |                   ^
    | PowerShell Cmds   | Results
    v                   |
+--------------------------------------------------+
|                                                  |
|            Microsoft Teams PowerShell            |
|                                                  |
+--------------------------------------------------+
```

## 2. Elementy sieciowe aplikacji

Aplikacja TeamsManager zawiera następujące elementy sieciowe:

### 1. REST API

Lokalny serwer obsługujący zapytania HTTP, umożliwiający:
- Zarządzanie zespołami Teams (CRUD)
- Zarządzanie użytkownikami
- Wysyłanie wiadomości
- Dostęp do historii operacji

Przykładowe endpointy:
- `GET /api/teams` - pobieranie listy zespołów
- `POST /api/teams` - tworzenie nowego zespołu
- `PUT /api/teams/{id}` - aktualizacja zespołu
- `POST /api/teams/{id}/members` - dodawanie członków do zespołu
- `POST /api/teams/{id}/channels` - tworzenie kanału w zespole
- `POST /api/teams/{id}/archive` - archiwizacja zespołu
- `POST /api/messages` - wysyłanie wiadomości do zespołu

### 2. WebSockets

System powiadomień w czasie rzeczywistym realizowany za pomocą SignalR:
- Informowanie o statusie długotrwałych operacji
- Powiadomienia o zakończonych zadaniach
- Aktualizacja interfejsu użytkownika w czasie rzeczywistym

Przykładowe metody Huba:
- `OperationStarted(operationId, operationType)` - powiadomienie o rozpoczęciu operacji
- `OperationProgress(operationId, progressPercentage)` - aktualizacja postępu operacji
- `OperationCompleted(operationId, result)` - powiadomienie o zakończeniu operacji
- `TeamCreated(teamId, teamName)` - powiadomienie o utworzeniu nowego zespołu
- `MembersAdded(teamId, count)` - powiadomienie o dodaniu nowych członków

### 3. Synchronizacja między instancjami

Mechanizm komunikacji TCP/IP umożliwiający:
- Synchronizację stanu między różnymi instancjami aplikacji
- Wymianę informacji o wykonywanych operacjach
- Zapobieganie konfliktom przy równoległych operacjach

Implementacja:
- Protokół TCP/IP do komunikacji między instancjami
- Serwer nasłuchujący na określonym porcie
- Klient do łączenia się z innymi instancjami
- Mechanizm serializacji i deserializacji komunikatów
- System rozpoznawania i rozwiązywania konfliktów

## 3. Wykorzystane technologie

Aplikacja TeamsManager wykorzystuje następujące technologie:

### Framework i język programowania
- **.NET 8.0** - nowoczesna, cross-platformowa platforma programistyczna
- **C#** - język programowania używany we wszystkich projektach

### Interfejs użytkownika
- **WPF (Windows Presentation Foundation)** - framework do tworzenia aplikacji desktopowych
- **MaterialDesignThemes** - biblioteka komponentów UI z ciemnym motywem
- **MVVM (Model-View-ViewModel)** - wzorzec projektowy do organizacji kodu interfejsu

### API i komunikacja sieciowa
- **ASP.NET Core** - framework do tworzenia API i aplikacji webowych
- **SignalR** - biblioteka do komunikacji w czasie rzeczywistym (WebSockets)
- **REST** - styl architektury używany w API
- **JSON** - format danych używany w komunikacji

### Dostęp do danych
- **Entity Framework Core** - ORM do pracy z bazą danych
- **SQLite** - lekka, plikowa baza danych
- **Repository Pattern** - wzorzec dostępu do danych

### Integracja z PowerShell
- **System.Management.Automation** - biblioteka do integracji z PowerShell
- **Microsoft Teams PowerShell Module** - moduł PowerShell do zarządzania Teams
- **Exchange Online PowerShell V2** - moduł do zarządzania usługami Exchange Online

### Testowanie
- **xUnit** - framework do testów jednostkowych
- **Moq** - biblioteka do mockowania obiektów w testach
- **FluentAssertions** - biblioteka do asercji w testach

### Narzędzia
- **Visual Studio** - zintegrowane środowisko programistyczne
- **Git** - system kontroli wersji
- **NuGet** - menedżer pakietów

### Pakiety NuGet

#### TeamsManager.Core
- **System.Management.Automation** - integracja z PowerShell i wykonywanie skryptów
- **Microsoft.Extensions.DependencyInjection** - wstrzykiwanie zależności
- **Microsoft.Extensions.Logging** - system logowania

#### TeamsManager.Api
- **Microsoft.AspNetCore.SignalR** - komunikacja w czasie rzeczywistym (WebSockets)
- **Swashbuckle.AspNetCore** - dokumentacja API (Swagger/OpenAPI)
- **Microsoft.EntityFrameworkCore** - dostęp do danych (ORM)
- **Microsoft.EntityFrameworkCore.Sqlite** - provider bazy danych SQLite

#### TeamsManager.Data
- **Microsoft.EntityFrameworkCore** - główny pakiet Entity Framework Core
- **Microsoft.EntityFrameworkCore.Sqlite** - obsługa bazy danych SQLite
- **Microsoft.EntityFrameworkCore.Tools** - narzędzia CLI do migracji
- **Microsoft.EntityFrameworkCore.Design** - narzędzia projektowe dla EF Core

#### TeamsManager.UI
- **MaterialDesignThemes** - nowoczesny interfejs użytkownika z ciemnym motywem
- **Microsoft.AspNetCore.SignalR.Client** - klient SignalR dla powiadomień
- **System.Net.Http.Json** - uproszczona komunikacja HTTP z JSON
- **Microsoft.Extensions.DependencyInjection** - wstrzykiwanie zależności w WPF

## 4. Harmonogram i realizacja projektu

### Harmonogram

#### Tydzień 1 (początek maja)
- Konfiguracja projektu i repozytoriów
- Implementacja podstawowej integracji z PowerShell
- Szkielet interfejsu użytkownika

#### Tydzień 2
- Implementacja lokalnego REST API
- Podstawowe operacje na Teams
- Integracja UI z API

#### Tydzień 3
- WebSockets dla powiadomień
- System synchronizacji
- Rozbudowa funkcji zarządzania Teams

#### Tydzień 4
- Szablony i historia operacji
- Testowanie całości systemu
- Przygotowanie dokumentacji

### Wymagania wstępne
- Moduły PowerShell: Microsoft Teams PowerShell Module, Exchange Online PowerShell V2
- Uprawnienia administratora do Teams
- Środowisko deweloperskie .NET

### Korzyści projektu
- Darmowe rozwiązanie (bez dodatkowych licencji)
- Pełna customizacja interfejsu i funkcji
- Automatyzacja procesów zarządzania Teams
- Lokalne działanie bez publicznych punktów końcowych API
- Zaliczenie przedmiotu "Programowanie aplikacji sieciowych"

## Instrukcja instalacji i uruchomienia

### Wymagania systemowe
- Windows 10/11
- .NET 8.0 Runtime
- PowerShell 7.0 lub nowszy
- Zainstalowane moduły:
  - Microsoft Teams PowerShell Module
  - Exchange Online PowerShell V2

### Instalacja modułów PowerShell
```powershell
Install-Module -Name MicrosoftTeams
Install-Module -Name ExchangeOnlineManagement
```

### Uruchomienie aplikacji
1. Pobierz najnowszą wersję aplikacji
2. Rozpakuj archiwum do wybranego katalogu
3. Uruchom plik TeamsManager.UI.exe
4. W oknie logowania podaj dane administratora Teams
5. Po pomyślnym logowaniu interfejs aplikacji będzie gotowy do użycia

## Dokumentacja użytkowa

*Ta sekcja zostanie uzupełniona w miarę rozwijania interfejsu użytkownika.*

## Historia zmian i dziennik rozwoju

## Aktualny status implementacji 2025-05-28 godz. 12.00

### ✅ Zrealizowano
- Podstawowa struktura rozwiązania (4 projekty)
- Konfiguracja pakietów NuGet dla wszystkich projektów
- **PowerShellService** - główna klasa do integracji z PowerShell:
  - Połączenie z Microsoft Teams
  - Tworzenie nowych zespołów
  - Zarządzanie sesją PowerShell Runspace
  - Obsługa błędów i logowanie

### 🔄 W trakcie realizacji
- Modele danych dla zespołów i użytkowników
- REST API endpoints
- Interfejs użytkownika WPF

### 📋 Do zrealizowania
- WebSockets dla powiadomień w czasie rzeczywistym
- Baza danych SQLite i operacje CRUD
- Kompletny interfejs użytkownika
- Testy i finalna dokumentacja

## Aktualny status implementacji (aktualizacja po Bloku 1) 2025-05-28 godz. 14.30

### ✅ Zrealizowano - Modele domenowe i testy (Blok 1)
- **Podstawowa struktura rozwiązania** (4 projekty + projekt testowy)
- **Konfiguracja pakietów NuGet** z kompatybilnością .NET 8.0
- **PowerShellService** - integracja z PowerShell i Microsoft Teams
- **Modele domenowe** (Domain-Driven Design):
  - `Team` - zespół Microsoft Teams z członkami i kanałami
  - `TeamMember` - członek zespołu z rolą (Owner/Member)
  - `Channel` - kanał w zespole
  - `TeamMemberRole` - enum z rolami użytkowników
- **Kompleksowe testy jednostkowe** (21 testów):
  - Testy modeli podstawowych (Team, TeamMember, Channel)
  - Testy enum z konwersjami i walidacją
  - Testy integracyjne relacji między obiektami
  - Pattern AAA (Arrange-Act-Assert)
  - FluentAssertions dla czytelnych asercji
  - Testy parametryczne z Theory/InlineData
  - **Pokrycie testowe: 100% modeli domenowych**

### 🔄 W trakcie realizacji - Warstwa danych (Blok 2)
- Entity Framework Core DbContext
- Migracje bazy danych SQLite
- Repository Pattern dla dostępu do danych

### 📋 Następne bloki do realizacji
- REST API z kontrolerami (Blok 3)
- WebSockets i SignalR (Blok 4)
- Interfejs użytkownika WPF (Blok 5)
- Zaawansowane funkcje i synchronizacja (Bloki 6-7)

### 🧪 Strategia testowania
- **Test-Driven Development (TDD)** - testy przed implementacją
- **Poziomy testowania**:
  - Unit tests - pojedyncze klasy i metody
  - Integration tests - relacje między komponentami
  - End-to-end tests - pełne scenariusze (planowane)
- **Narzędzia**: xUnit, FluentAssertions, Moq
- **Wyniki**: 21/21 testów przechodzi (100% success rate)

## Autorzy i licencja

Projekt realizowany jako zaliczenie przedmiotu "Programowanie aplikacji sieciowych".

Licencja: MIT