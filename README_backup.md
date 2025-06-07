# TeamsManager

> **🎓 Projekt Dyplomowy - System zarządzania zespołami Microsoft Teams**  
> **👨‍💻 Autor:** Mariusz Jaguścik  
> **🏫 Uczelnia:** Akademia Ekonomiczno-Humanistyczna  
> **📅 Ukończony:** 6 czerwca 2025  

## 📖 Dokumentacja

**📋 Pełna dokumentacja projektu znajduje się w pliku:**  
**[docs/README.md](docs/README.md)**

## ⚡ Szybki Start

1. **Wymagania:** .NET 9.0, Visual Studio 2022, Azure AD
2. **Kompilacja:** `dotnet build --configuration Release`
3. **Testy:** `961/961 testów przechodzi ✅`
4. **Uruchomienie API:** `cd TeamsManager.Api && dotnet run`
5. **Uruchomienie UI:** `cd TeamsManager.UI && dotnet run`

## 📊 Status Projektu

| Komponent | Status | Pokrycie |
|-----------|--------|----------|
| 🏗️ **Core Domain** | ✅ 100% | ✅ Pełne |
| 🗄️ **Data Layer** | ✅ 100% | ✅ Pełne |
| 🌐 **REST API** | ✅ 95% | ✅ Wysokie |
| 🖥️ **Desktop UI** | 🔄 80% | ⚠️ Częściowe |
| 🧪 **Testy** | ✅ 961/961 | ✅ 100% |

## 🏆 Kluczowe Funkcjonalności

- ✅ **Zarządzanie zespołami Teams** z Microsoft Graph API
- ✅ **Hierarchiczne struktury organizacyjne** (działy, szkoły)
- ✅ **Dynamiczne szablony nazw** zespołów
- ✅ **Inteligentna synchronizacja** Graph-DB
- ✅ **System audytu** i logowania operacji
- ✅ **REST API** z JWT authentication
- ✅ **SignalR Hub** dla powiadomień real-time
- ✅ **Orkiestrator procesów szkolnych** - automatyzacja kompleksowych workflow
  - 🔄 Tworzenie zespołów dla nowego roku szkolnego (batch processing)
  - 📦 Archiwizacja zespołów z poprzedniego roku 
  - 🔀 Płynne przejście między latami szkolnymi (transition workflow)
  - 📊 Monitoring i anulowanie długotrwałych procesów
  - 🛡️ Thread-safe operations z semaphore limiting
- ✅ **Orkiestrator importu danych** - zorganizowany workflow importu CSV/Excel **(NOWY - 2025-06-07)**
  - 📂 Import użytkowników z plików CSV z walidacją biznesową
  - 📊 Import zespołów z plików Excel z mapowaniem kolumn
  - 🏫 Import struktury szkoły (działy, przedmioty, szablony)
  - ✔️ 7-etapowy workflow: walidacja → parsing → dry-run → batch processing → rollback → reporting
  - 📋 Generowanie szablonów importu (CSV, Excel, JSON)
  - 🎯 Monitoring i anulowanie procesów importu w czasie rzeczywistym
  - 🛡️ Error handling z AcceptableErrorPercentage i ContinueOnError
- ✅ **Orkiestrator cyklu życia zespołów** - kompleksowe zarządzanie lifecycle Teams **(NOWY - 2025-06-07)**
  - 🗃️ Masowa archiwizacja zespołów z opcjonalnym cleanup (członkowie, kanały, pliki)
  - 🔄 Masowe przywracanie zespołów z walidacją właścicieli
  - 📦 Migracja zespołów między latami szkolnymi z kopiowaniem struktur
  - 🧹 Konsolidacja nieaktywnych zespołów (automatyczna archiwizacja)
  - 📊 Thread-safe batch processing z SemaphoreSlim(3,3) dla równoległych operacji
  - 💬 Powiadomienia właścicieli przed archiwizacją
  - 🎯 Real-time monitoring procesów z możliwością anulowania
  - 🛡️ Resilient error handling z progami błędów i rollback
- ✅ **Orkiestrator zarządzania użytkownikami** - kompleksowe operacje HR i masowe działania **(NOWY - 2025-06-07)**
  - 👤 Masowy onboarding użytkowników z 7-etapowym workflow (walidacja → tworzenie → zespoły → typy szkół → przedmioty → powiadomienia → finalizacja)
  - 👋 Masowy offboarding użytkowników z transferem własności zespołów i backup danych
  - 🔄 Masowe zmiany ról użytkowników w systemie z walidacją uprawnień
  - 👥 Masowe operacje członkostwa w zespołach Teams (dodawanie/usuwanie z wielu zespołów)
  - 📊 Thread-safe processing z SemaphoreSlim(3,3) dla 3 równoległych procesów
  - 🎯 Real-time monitoring i anulowanie procesów zarządzania użytkownikami
  - 🛡️ Helper methods: ValidateOnboardingPlan, ProcessOnboardingBatch, TransferTeamOwnership
  - 💾 Proper resource cleanup z finally blocks i cancellation tokens

## 📄 Licencja

MIT License

---

**🔗 [Pełna dokumentacja w docs/README.md](docs/README.md)**
