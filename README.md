# 🏫 TeamsManager

> **System zarządzania zespołami Microsoft Teams dla środowisk edukacyjnych**

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Tests](https://img.shields.io/badge/Tests-1113%2F1113%20%E2%9C%85-brightgreen.svg)](TeamsManager.Tests)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**TeamsManager** to zaawansowany system do automatyzacji zarządzania zespołami Microsoft Teams, stworzony specjalnie dla szkół i uczelni. Łączy w sobie lokalną aplikację desktopową WPF z potężnym REST API, zapewniając pełną kontrolę nad organizacją cyfrowej przestrzeni edukacyjnej.

## ✨ Kluczowe funkcjonalności

🏗️ **Clean Architecture** - DDD + Application Layer wzorce projektowe  
🔗 **Microsoft Graph** - integracja z pełnym przepływem OAuth2 OBO  
📊 **Masowe operacje** - 6 zaawansowanych orkiestratorów enterprise-grade  
🗄️ **Lokalna baza** - SQLite z Entity Framework Core  
🖥️ **Nowoczesny UI** - WPF + MaterialDesign 3.0  
🌐 **REST API** - ASP.NET Core z JWT authentication  
🧪 **100% testów** - 1113 testów jednostkowych i integracyjnych  

## 🚀 Szybki start

### Wymagania
- **.NET 9.0 SDK**
- **Visual Studio 2022** (17.8+)
- **Azure AD tenant** z Microsoft Graph permissions

### Instalacja
```bash
git clone https://github.com/JaegerMajster/TeamsManager.git
cd TeamsManager
dotnet build --configuration Release
```

### Uruchomienie
```bash
# API Server
cd TeamsManager.Api
dotnet run --urls http://localhost:5000

# Desktop UI
cd TeamsManager.UI  
dotnet run
```

## 📋 Dokumentacja

| Dokument | Opis |
|----------|------|
| **[📚 Dokumentacja Techniczna](docs/dokTech.md)** | Pełna dokumentacja architektury, implementacji i wzorców |
| **[🏗️ Struktura Projektu](docs/strukturaProjektu.md)** | Szczegółowa struktura plików i komponentów |
| **[🎨 Przewodnik Stylów UI](docs/styleUI.md)** | Standardy MaterialDesign i guidelines UX |
| **[🔄 System Synchronizacji](docs/synchronizacja.md)** | Mechanizmy Graph-DB sync i cache |
| **[🛡️ Strategia Cache](docs/strategiaCache.md)** | Optymalizacja wydajności i zarządzanie pamięcią |
| **[⚙️ PowerShell Services](docs/powerShellService.md)** | Integracja z Microsoft Graph PowerShell |

## 🎯 Obszary zastosowania

### 🏫 Edukacja
- Automatyczne tworzenie zespołów dla klas i przedmiotów
- Zarządzanie latami szkolnymi z archiwizacją
- Masowy import studentów i nauczycieli
- Hierarchiczne struktury organizacyjne (działy, szkoły)

### 🏢 Enterprise HR
- Masowy onboarding/offboarding pracowników  
- Zarządzanie rolami i uprawnieniami
- Operacje członkostwa w zespołach
- Audyt i monitoring działań administracyjnych

## 🔧 Architektura

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   WPF Desktop   │ ── │   REST API      │ ── │ Microsoft Graph │
│   Material UI   │    │   JWT + OBO     │    │   Teams API     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                        │
         │              ┌─────────────────┐
         └────────────── │   SQLite DB     │
                         │   EF Core       │
                         └─────────────────┘
```

## 📊 Status projektu

| Komponent | Implementacja | Testy |
|-----------|:------------:|:-----:|
| Core Domain | ✅ 100% | ✅ Pełne |
| Data Layer | ✅ 100% | ✅ Pełne | 
| REST API | ✅ 95% | ✅ Wysokie |
| Desktop UI | 🔄 80% | ⚠️ Częściowe |
| **Łącznie** | **✅ 1113/1113** | **✅ 100%** |

## 🏆 Zaawansowane orkiestratory

🏫 **Procesy szkolne** - zarządzanie latami szkolnymi  
📂 **Import danych** - CSV/Excel z walidacją biznesową  
🔄 **Cykl życia zespołów** - archiwizacja i migracja  
👥 **Zarządzanie użytkownikami** - masowy HR workflow  
🏥 **Monitorowanie zdrowia** - diagnostyka i auto-naprawa systemu  
📊 **Raportowanie** - generowanie raportów i eksport danych systemowych  

*Wszystkie orkiestratory oferują thread-safe processing, real-time monitoring i graceful cancellation.*

## 🤝 Autorzy

**Mariusz Jaguścik**  
📧 Email: [jaguscikm@gmail.com](mailto:jaguscikm@gmail.com)  
🏫 Akademia Ekonomiczno-Humanistyczna w Warszawie 
🎓 Projekt studencki - Programowanie aplikacji sieciowych, Programowanie w .NET, Projektowanie zaawansowanych systemów informatycznych  

## 📄 Licencja

Ten projekt jest licencjonowany na warunkach [MIT License](LICENSE).
