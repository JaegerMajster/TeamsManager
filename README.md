# TeamsManager - Dokumentacja projektu

## Informacje ogólne

TeamsManager to aplikacja lokalnie uruchamiana na komputerze, która dzia³a jako zaawansowana nak³adka na PowerShell do zarz¹dzania grupami Microsoft Teams. Zamiast korzystaæ z licencjonowanego Microsoft Graph, aplikacja wykorzystuje darmowe skrypty PowerShell, oferuj¹c przy tym bogat¹ funkcjonalnoœæ i przyjazny interfejs u¿ytkownika.

Projekt realizowany jest jako zaliczenie przedmiotu "Programowanie aplikacji sieciowych".

## 1. Architektura aplikacji

### Struktura rozwi¹zania

Rozwi¹zanie TeamsManager sk³ada siê z czterech g³ównych projektów, które razem tworz¹ kompletne rozwi¹zanie do zarz¹dzania grupami Microsoft Teams.

#### TeamsManager.Core

Biblioteka zawieraj¹ca g³ówn¹ logikê biznesow¹ aplikacji. Odpowiada za:
- Integracjê z PowerShell i wykonywanie poleceñ
- Zarz¹dzanie grupami Teams (tworzenie, modyfikowanie, archiwizowanie)
- Operacje na u¿ytkownikach (dodawanie, usuwanie)
- Definiowanie g³ównych interfejsów i modeli danych

Kluczowe klasy:
- `PowerShellService` - klasa do komunikacji z PowerShell
- `TeamsService` - klasa obs³uguj¹ca operacje na zespo³ach Teams
- `UserService` - klasa obs³uguj¹ca operacje na u¿ytkownikach Teams

#### TeamsManager.Api

Lokalny serwer REST API, który udostêpnia funkcjonalnoœæ Core poprzez HTTP. Odpowiada za:
- Udostêpnianie endpointów do zarz¹dzania Teams
- Obs³ugê WebSockets do powiadomieñ w czasie rzeczywistym
- Komunikacjê z innymi instancjami aplikacji
- Uwierzytelnianie i autoryzacjê zapytañ

Kluczowe komponenty:
- Kontrolery API (TeamsController, UsersController, TemplatesController)
- Hub SignalR dla powiadomieñ w czasie rzeczywistym
- Middleware do uwierzytelniania
- Serwisy hostuj¹ce d³ugotrwa³e operacje

#### TeamsManager.UI

Aplikacja WPF stanowi¹ca interfejs u¿ytkownika. Odpowiada za:
- Prezentacjê danych i formularzy do zarz¹dzania Teams
- Komunikacjê z API
- Odbieranie powiadomieñ w czasie rzeczywistym
- Prezentacjê postêpu d³ugotrwa³ych operacji

Kluczowe elementy:
- G³ówne okno aplikacji z nawigacj¹
- Widoki dla zarz¹dzania zespo³ami, kana³ami i u¿ytkownikami
- Formularze do wprowadzania danych
- Komponenty do wyœwietlania powiadomieñ i postêpu

#### TeamsManager.Data

Biblioteka do zarz¹dzania dostêpem do danych. Odpowiada za:
- Przechowywanie szablonów zespo³ów
- Logowanie historii operacji
- Przechowywanie konfiguracji u¿ytkownika
- Obs³ugê lokalnej bazy danych SQLite

Kluczowe elementy:
- DbContext dla Entity Framework Core
- Modele danych (TeamTemplate, OperationHistory)
- Repozytoria do obs³ugi operacji na danych
- Migracje bazy danych

### Diagramy i schematy

#### Diagram przep³ywu danych

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

#### Diagram komunikacji miêdzy komponentami

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

Aplikacja TeamsManager zawiera nastêpuj¹ce elementy sieciowe:

### 1. REST API

Lokalny serwer obs³uguj¹cy zapytania HTTP, umo¿liwiaj¹cy:
- Zarz¹dzanie zespo³ami Teams (CRUD)
- Zarz¹dzanie u¿ytkownikami
- Wysy³anie wiadomoœci
- Dostêp do historii operacji

Przyk³adowe endpointy:
- `GET /api/teams` - pobieranie listy zespo³ów
- `POST /api/teams` - tworzenie nowego zespo³u
- `PUT /api/teams/{id}` - aktualizacja zespo³u
- `POST /api/teams/{id}/members` - dodawanie cz³onków do zespo³u
- `POST /api/teams/{id}/channels` - tworzenie kana³u w zespole
- `POST /api/teams/{id}/archive` - archiwizacja zespo³u
- `POST /api/messages` - wysy³anie wiadomoœci do zespo³u

### 2. WebSockets

System powiadomieñ w czasie rzeczywistym realizowany za pomoc¹ SignalR:
- Informowanie o statusie d³ugotrwa³ych operacji
- Powiadomienia o zakoñczonych zadaniach
- Aktualizacja interfejsu u¿ytkownika w czasie rzeczywistym

Przyk³adowe metody Huba:
- `OperationStarted(operationId, operationType)` - powiadomienie o rozpoczêciu operacji
- `OperationProgress(operationId, progressPercentage)` - aktualizacja postêpu operacji
- `OperationCompleted(operationId, result)` - powiadomienie o zakoñczeniu operacji
- `TeamCreated(teamId, teamName)` - powiadomienie o utworzeniu nowego zespo³u
- `MembersAdded(teamId, count)` - powiadomienie o dodaniu nowych cz³onków

### 3. Synchronizacja miêdzy instancjami

Mechanizm komunikacji TCP/IP umo¿liwiaj¹cy:
- Synchronizacjê stanu miêdzy ró¿nymi instancjami aplikacji
- Wymianê informacji o wykonywanych operacjach
- Zapobieganie konfliktom przy równoleg³ych operacjach

Implementacja:
- Protokó³ TCP/IP do komunikacji miêdzy instancjami
- Serwer nas³uchuj¹cy na okreœlonym porcie
- Klient do ³¹czenia siê z innymi instancjami
- Mechanizm serializacji i deserializacji komunikatów
- System rozpoznawania i rozwi¹zywania konfliktów

## 3. Wykorzystane technologie

Aplikacja TeamsManager wykorzystuje nastêpuj¹ce technologie:

### Framework i jêzyk programowania
- **.NET 8.0** - nowoczesna, cross-platformowa platforma programistyczna
- **C#** - jêzyk programowania u¿ywany we wszystkich projektach

### Interfejs u¿ytkownika
- **WPF (Windows Presentation Foundation)** - framework do tworzenia aplikacji desktopowych
- **MaterialDesignThemes** - biblioteka komponentów UI z ciemnym motywem
- **MVVM (Model-View-ViewModel)** - wzorzec projektowy do organizacji kodu interfejsu

### API i komunikacja sieciowa
- **ASP.NET Core** - framework do tworzenia API i aplikacji webowych
- **SignalR** - biblioteka do komunikacji w czasie rzeczywistym (WebSockets)
- **REST** - styl architektury u¿ywany w API
- **JSON** - format danych u¿ywany w komunikacji

### Dostêp do danych
- **Entity Framework Core** - ORM do pracy z baz¹ danych
- **SQLite** - lekka, plikowa baza danych
- **Repository Pattern** - wzorzec dostêpu do danych

### Integracja z PowerShell
- **System.Management.Automation** - biblioteka do integracji z PowerShell
- **Microsoft Teams PowerShell Module** - modu³ PowerShell do zarz¹dzania Teams
- **Exchange Online PowerShell V2** - modu³ do zarz¹dzania us³ugami Exchange Online

### Testowanie
- **xUnit** - framework do testów jednostkowych
- **Moq** - biblioteka do mockowania obiektów w testach
- **FluentAssertions** - biblioteka do asercji w testach

### Narzêdzia
- **Visual Studio** - zintegrowane œrodowisko programistyczne
- **Git** - system kontroli wersji
- **NuGet** - mened¿er pakietów

## 4. Harmonogram i realizacja projektu

### Harmonogram

#### Tydzieñ 1 (pocz¹tek maja)
- Konfiguracja projektu i repozytoriów
- Implementacja podstawowej integracji z PowerShell
- Szkielet interfejsu u¿ytkownika

#### Tydzieñ 2
- Implementacja lokalnego REST API
- Podstawowe operacje na Teams
- Integracja UI z API

#### Tydzieñ 3
- WebSockets dla powiadomieñ
- System synchronizacji
- Rozbudowa funkcji zarz¹dzania Teams

#### Tydzieñ 4
- Szablony i historia operacji
- Testowanie ca³oœci systemu
- Przygotowanie dokumentacji

### Wymagania wstêpne
- Modu³y PowerShell: Microsoft Teams PowerShell Module, Exchange Online PowerShell V2
- Uprawnienia administratora do Teams
- Œrodowisko deweloperskie .NET

### Korzyœci projektu
- Darmowe rozwi¹zanie (bez dodatkowych licencji)
- Pe³na customizacja interfejsu i funkcji
- Automatyzacja procesów zarz¹dzania Teams
- Lokalne dzia³anie bez publicznych punktów koñcowych API
- Zaliczenie przedmiotu "Programowanie aplikacji sieciowych"

## Instrukcja instalacji i uruchomienia

### Wymagania systemowe
- Windows 10/11
- .NET 8.0 Runtime
- PowerShell 7.0 lub nowszy
- Zainstalowane modu³y:
  - Microsoft Teams PowerShell Module
  - Exchange Online PowerShell V2

### Instalacja modu³ów PowerShell
```powershell
Install-Module -Name MicrosoftTeams
Install-Module -Name ExchangeOnlineManagement
```

### Uruchomienie aplikacji
1. Pobierz najnowsz¹ wersjê aplikacji
2. Rozpakuj archiwum do wybranego katalogu
3. Uruchom plik TeamsManager.UI.exe
4. W oknie logowania podaj dane administratora Teams
5. Po pomyœlnym logowaniu interfejs aplikacji bêdzie gotowy do u¿ycia

## Dokumentacja u¿ytkowa

*Ta sekcja zostanie uzupe³niona w miarê rozwijania interfejsu u¿ytkownika.*

## Historia zmian i dziennik rozwoju

*Ta sekcja bêdzie aktualizowana w trakcie rozwoju projektu.*

## Autorzy i licencja

Projekt realizowany jako zaliczenie przedmiotu "Programowanie aplikacji sieciowych".

Licencja: MIT