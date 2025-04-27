# TeamsManager - Dokumentacja projektu

## Informacje og�lne

TeamsManager to aplikacja lokalnie uruchamiana na komputerze, kt�ra dzia�a jako zaawansowana nak�adka na PowerShell do zarz�dzania grupami Microsoft Teams. Zamiast korzysta� z licencjonowanego Microsoft Graph, aplikacja wykorzystuje darmowe skrypty PowerShell, oferuj�c przy tym bogat� funkcjonalno�� i przyjazny interfejs u�ytkownika.

Projekt realizowany jest jako zaliczenie przedmiotu "Programowanie aplikacji sieciowych".

## 1. Architektura aplikacji

### Struktura rozwi�zania

Rozwi�zanie TeamsManager sk�ada si� z czterech g��wnych projekt�w, kt�re razem tworz� kompletne rozwi�zanie do zarz�dzania grupami Microsoft Teams.

#### TeamsManager.Core

Biblioteka zawieraj�ca g��wn� logik� biznesow� aplikacji. Odpowiada za:
- Integracj� z PowerShell i wykonywanie polece�
- Zarz�dzanie grupami Teams (tworzenie, modyfikowanie, archiwizowanie)
- Operacje na u�ytkownikach (dodawanie, usuwanie)
- Definiowanie g��wnych interfejs�w i modeli danych

Kluczowe klasy:
- `PowerShellService` - klasa do komunikacji z PowerShell
- `TeamsService` - klasa obs�uguj�ca operacje na zespo�ach Teams
- `UserService` - klasa obs�uguj�ca operacje na u�ytkownikach Teams

#### TeamsManager.Api

Lokalny serwer REST API, kt�ry udost�pnia funkcjonalno�� Core poprzez HTTP. Odpowiada za:
- Udost�pnianie endpoint�w do zarz�dzania Teams
- Obs�ug� WebSockets do powiadomie� w czasie rzeczywistym
- Komunikacj� z innymi instancjami aplikacji
- Uwierzytelnianie i autoryzacj� zapyta�

Kluczowe komponenty:
- Kontrolery API (TeamsController, UsersController, TemplatesController)
- Hub SignalR dla powiadomie� w czasie rzeczywistym
- Middleware do uwierzytelniania
- Serwisy hostuj�ce d�ugotrwa�e operacje

#### TeamsManager.UI

Aplikacja WPF stanowi�ca interfejs u�ytkownika. Odpowiada za:
- Prezentacj� danych i formularzy do zarz�dzania Teams
- Komunikacj� z API
- Odbieranie powiadomie� w czasie rzeczywistym
- Prezentacj� post�pu d�ugotrwa�ych operacji

Kluczowe elementy:
- G��wne okno aplikacji z nawigacj�
- Widoki dla zarz�dzania zespo�ami, kana�ami i u�ytkownikami
- Formularze do wprowadzania danych
- Komponenty do wy�wietlania powiadomie� i post�pu

#### TeamsManager.Data

Biblioteka do zarz�dzania dost�pem do danych. Odpowiada za:
- Przechowywanie szablon�w zespo��w
- Logowanie historii operacji
- Przechowywanie konfiguracji u�ytkownika
- Obs�ug� lokalnej bazy danych SQLite

Kluczowe elementy:
- DbContext dla Entity Framework Core
- Modele danych (TeamTemplate, OperationHistory)
- Repozytoria do obs�ugi operacji na danych
- Migracje bazy danych

### Diagramy i schematy

#### Diagram przep�ywu danych

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

#### Diagram komunikacji mi�dzy komponentami

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

Aplikacja TeamsManager zawiera nast�puj�ce elementy sieciowe:

### 1. REST API

Lokalny serwer obs�uguj�cy zapytania HTTP, umo�liwiaj�cy:
- Zarz�dzanie zespo�ami Teams (CRUD)
- Zarz�dzanie u�ytkownikami
- Wysy�anie wiadomo�ci
- Dost�p do historii operacji

Przyk�adowe endpointy:
- `GET /api/teams` - pobieranie listy zespo��w
- `POST /api/teams` - tworzenie nowego zespo�u
- `PUT /api/teams/{id}` - aktualizacja zespo�u
- `POST /api/teams/{id}/members` - dodawanie cz�onk�w do zespo�u
- `POST /api/teams/{id}/channels` - tworzenie kana�u w zespole
- `POST /api/teams/{id}/archive` - archiwizacja zespo�u
- `POST /api/messages` - wysy�anie wiadomo�ci do zespo�u

### 2. WebSockets

System powiadomie� w czasie rzeczywistym realizowany za pomoc� SignalR:
- Informowanie o statusie d�ugotrwa�ych operacji
- Powiadomienia o zako�czonych zadaniach
- Aktualizacja interfejsu u�ytkownika w czasie rzeczywistym

Przyk�adowe metody Huba:
- `OperationStarted(operationId, operationType)` - powiadomienie o rozpocz�ciu operacji
- `OperationProgress(operationId, progressPercentage)` - aktualizacja post�pu operacji
- `OperationCompleted(operationId, result)` - powiadomienie o zako�czeniu operacji
- `TeamCreated(teamId, teamName)` - powiadomienie o utworzeniu nowego zespo�u
- `MembersAdded(teamId, count)` - powiadomienie o dodaniu nowych cz�onk�w

### 3. Synchronizacja mi�dzy instancjami

Mechanizm komunikacji TCP/IP umo�liwiaj�cy:
- Synchronizacj� stanu mi�dzy r�nymi instancjami aplikacji
- Wymian� informacji o wykonywanych operacjach
- Zapobieganie konfliktom przy r�wnoleg�ych operacjach

Implementacja:
- Protok� TCP/IP do komunikacji mi�dzy instancjami
- Serwer nas�uchuj�cy na okre�lonym porcie
- Klient do ��czenia si� z innymi instancjami
- Mechanizm serializacji i deserializacji komunikat�w
- System rozpoznawania i rozwi�zywania konflikt�w

## 3. Wykorzystane technologie

Aplikacja TeamsManager wykorzystuje nast�puj�ce technologie:

### Framework i j�zyk programowania
- **.NET 8.0** - nowoczesna, cross-platformowa platforma programistyczna
- **C#** - j�zyk programowania u�ywany we wszystkich projektach

### Interfejs u�ytkownika
- **WPF (Windows Presentation Foundation)** - framework do tworzenia aplikacji desktopowych
- **MaterialDesignThemes** - biblioteka komponent�w UI z ciemnym motywem
- **MVVM (Model-View-ViewModel)** - wzorzec projektowy do organizacji kodu interfejsu

### API i komunikacja sieciowa
- **ASP.NET Core** - framework do tworzenia API i aplikacji webowych
- **SignalR** - biblioteka do komunikacji w czasie rzeczywistym (WebSockets)
- **REST** - styl architektury u�ywany w API
- **JSON** - format danych u�ywany w komunikacji

### Dost�p do danych
- **Entity Framework Core** - ORM do pracy z baz� danych
- **SQLite** - lekka, plikowa baza danych
- **Repository Pattern** - wzorzec dost�pu do danych

### Integracja z PowerShell
- **System.Management.Automation** - biblioteka do integracji z PowerShell
- **Microsoft Teams PowerShell Module** - modu� PowerShell do zarz�dzania Teams
- **Exchange Online PowerShell V2** - modu� do zarz�dzania us�ugami Exchange Online

### Testowanie
- **xUnit** - framework do test�w jednostkowych
- **Moq** - biblioteka do mockowania obiekt�w w testach
- **FluentAssertions** - biblioteka do asercji w testach

### Narz�dzia
- **Visual Studio** - zintegrowane �rodowisko programistyczne
- **Git** - system kontroli wersji
- **NuGet** - mened�er pakiet�w

## 4. Harmonogram i realizacja projektu

### Harmonogram

#### Tydzie� 1 (pocz�tek maja)
- Konfiguracja projektu i repozytori�w
- Implementacja podstawowej integracji z PowerShell
- Szkielet interfejsu u�ytkownika

#### Tydzie� 2
- Implementacja lokalnego REST API
- Podstawowe operacje na Teams
- Integracja UI z API

#### Tydzie� 3
- WebSockets dla powiadomie�
- System synchronizacji
- Rozbudowa funkcji zarz�dzania Teams

#### Tydzie� 4
- Szablony i historia operacji
- Testowanie ca�o�ci systemu
- Przygotowanie dokumentacji

### Wymagania wst�pne
- Modu�y PowerShell: Microsoft Teams PowerShell Module, Exchange Online PowerShell V2
- Uprawnienia administratora do Teams
- �rodowisko deweloperskie .NET

### Korzy�ci projektu
- Darmowe rozwi�zanie (bez dodatkowych licencji)
- Pe�na customizacja interfejsu i funkcji
- Automatyzacja proces�w zarz�dzania Teams
- Lokalne dzia�anie bez publicznych punkt�w ko�cowych API
- Zaliczenie przedmiotu "Programowanie aplikacji sieciowych"

## Instrukcja instalacji i uruchomienia

### Wymagania systemowe
- Windows 10/11
- .NET 8.0 Runtime
- PowerShell 7.0 lub nowszy
- Zainstalowane modu�y:
  - Microsoft Teams PowerShell Module
  - Exchange Online PowerShell V2

### Instalacja modu��w PowerShell
```powershell
Install-Module -Name MicrosoftTeams
Install-Module -Name ExchangeOnlineManagement
```

### Uruchomienie aplikacji
1. Pobierz najnowsz� wersj� aplikacji
2. Rozpakuj archiwum do wybranego katalogu
3. Uruchom plik TeamsManager.UI.exe
4. W oknie logowania podaj dane administratora Teams
5. Po pomy�lnym logowaniu interfejs aplikacji b�dzie gotowy do u�ycia

## Dokumentacja u�ytkowa

*Ta sekcja zostanie uzupe�niona w miar� rozwijania interfejsu u�ytkownika.*

## Historia zmian i dziennik rozwoju

*Ta sekcja b�dzie aktualizowana w trakcie rozwoju projektu.*

## Autorzy i licencja

Projekt realizowany jako zaliczenie przedmiotu "Programowanie aplikacji sieciowych".

Licencja: MIT