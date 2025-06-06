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

## 📄 Licencja

MIT License

---

**🔗 [Pełna dokumentacja w docs/README.md](docs/README.md)**
