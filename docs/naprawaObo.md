# Naprawa przepływu OAuth2 On-Behalf-Of (OBO) w TeamsManager

## 🔍 **DOKŁADNA ANALIZA OBECNEGO STANU PRZEPŁYWU OBO**

### **1. GŁÓWNY PROBLEM: Nieprawidłowa architektura OBO flow w EmbeddedApiServer**

Obecnie **GraphConnectionService** w EmbeddedApiServer próbuje używać **Client Credentials flow** zamiast **OBO flow**:

```csharp
// GraphConnectionService.cs:1303-1334
public async Task<string?> GetAccessTokenAsync()
{
    // ❌ PROBLEM: Używa Client Credentials zamiast OBO
    var scopes = _graphConfig.Scopes.ClientCredentials;
    var result = await _confidentialClientApp
        .AcquireTokenForClient(scopes)  // ❌ To jest Client Credentials!
        .ExecuteAsync();
}
```

### **2. PROBLEM: TokenManager w EmbeddedApiServer nie obsługuje OBO**

TokenManager w Core został zaprojektowany dla głównej aplikacji API, ale w EmbeddedApiServer:
- Nie ma dostępu do tokenu użytkownika z UI
- Nie ma mechanizmu przekazywania tokenu dla OBO
- Próbuje używać Client Credentials zamiast delegowanych uprawnień

### **3. PROBLEM: ModernHttpService używa względnych URL-i bez BaseAddress**

Z logów:
```
ERROR: An invalid request URI was provided. Either the request URI must be an absolute URI or BaseAddress must be set.
```

ModernHttpService wywołuje endpointy jak `/me` bez ustawienia BaseAddress.

### **4. PROBLEM: Nieprawidłowy przepływ uwierzytelniania w EmbeddedDiagnosticsController**

Kontroler próbuje używać GraphConnectionService, który nie ma dostępu do tokenu użytkownika.

## 🌐 **NAJLEPSZE PRAKTYKI OBO FLOW Z INTERNETU (2025)**

### **Architektura OBO dla Embedded API Server**

1. **Token Flow**:
   ```
   UI App → Uzyskuje token użytkownika
   UI App → Przekazuje token do EmbeddedApiServer
   EmbeddedApiServer → Używa OBO do uzyskania tokenu dla Graph API
   ```

2. **Kluczowe komponenty**:
   - **Token Validator** - waliduje token z UI
   - **OBO Token Manager** - wykonuje exchange tokenu
   - **Graph Client** - używa tokenu OBO do wywołań Graph API

3. **Kolejność operacji**:
   ```
   1. UI → Uwierzytelnia użytkownika (MSAL)
   2. UI → Uzyskuje access token
   3. UI → Wysyła żądanie do EmbeddedApiServer z tokenem w Authorization header
   4. EmbeddedApiServer → Waliduje token
   5. EmbeddedApiServer → Wykonuje OBO exchange (AcquireTokenOnBehalfOf)
   6. EmbeddedApiServer → Używa tokenu OBO do wywołań Graph API
   ```

### **Konfiguracja MSAL dla OBO**

```csharp
var result = await confidentialClientApp
    .AcquireTokenOnBehalfOf(scopes, userAssertion)
    .ExecuteAsync();
```

Gdzie `userAssertion` to token otrzymany z UI.

## 🔧 **PLAN NAPRAWY**

### **Etap 1: Naprawa ModernHttpService**
- [ ] Dodać obsługę BaseAddress dla Graph API
- [ ] Naprawić względne URL-e

### **Etap 2: Implementacja OBO Token Manager dla EmbeddedApiServer**
- [ ] Utworzyć `EmbeddedOboTokenManager`
- [ ] Implementować `AcquireTokenOnBehalfOf`
- [ ] Dodać walidację tokenu z UI

### **Etap 3: Modyfikacja EmbeddedDiagnosticsController**
- [ ] Dodać odczyt tokenu z Authorization header
- [ ] Przekazać token do OBO Token Manager
- [ ] Używać tokenu OBO dla Graph API calls

### **Etap 4: Konfiguracja DI w EmbeddedApiServer**
- [ ] Zarejestrować EmbeddedOboTokenManager
- [ ] Skonfigurować GraphConnectionService dla OBO
- [ ] Dodać middleware dla token validation

### **Etap 5: Modyfikacja UI**
- [ ] Dodać przekazywanie tokenu do EmbeddedApiServer
- [ ] Implementować refresh token logic

## 📊 **ANALIZA LOGÓW - ZIDENTYFIKOWANE PROBLEMY**

### **Logi Error:**
```
ERROR: An invalid request URI was provided. Either the request URI must be an absolute URI or BaseAddress must be set.
```
**Przyczyna**: ModernHttpService używa względnych URL-i bez BaseAddress

### **Logi Warning:**
```
WARN: Brak kont w cache tokenu
WARN: Żądanie Graph API nie powiodło się. Endpoint: /me, StatusCode: BadRequest
```
**Przyczyna**: GraphConnectionService próbuje używać Client Credentials zamiast OBO

### **Logi Info:**
```
INFO: EmbeddedApiServer uruchomiony na porcie 5555
INFO: Rozpoczynanie diagnostyki połączenia Graph API
```
**Status**: EmbeddedApiServer działa, ale ma problemy z Graph API

## 🏗️ **PROPONOWANA ARCHITEKTURA**

```
┌─────────────────┐    HTTP Request     ┌─────────────────────┐
│                 │    + Bearer Token   │                     │
│   TeamsManager  ├────────────────────►│  EmbeddedApiServer  │
│       UI        │                     │                     │
└─────────────────┘                     └──────────┬──────────┘
                                                   │
                                                   │ OBO Exchange
                                                   ▼
                                        ┌─────────────────────┐
                                        │                     │
                                        │  Microsoft Graph    │
                                        │       API           │
                                        └─────────────────────┘
```

### **Komponenty do implementacji:**

1. **EmbeddedOboTokenManager**
   - Obsługa AcquireTokenOnBehalfOf
   - Cache tokenów OBO
   - Refresh token logic

2. **TokenValidationMiddleware**
   - Walidacja tokenów z UI
   - Extraction tokenu z Authorization header

3. **EmbeddedGraphService**
   - Wrapper dla Graph API calls z tokenem OBO
   - Error handling dla OBO flow

4. **OboConfiguration**
   - Konfiguracja scopes dla OBO
   - Azure AD app registration settings

## 🚨 **KRYTYCZNE UWAGI**

1. **Azure AD App Registration musi mieć:**
   - `api://[client-id]` jako Application ID URI
   - Delegated permissions dla Microsoft Graph
   - `access_as_user` scope

2. **UI musi żądać scope:**
   - `api://[embedded-api-client-id]/access_as_user`

3. **EmbeddedApiServer musi mieć:**
   - Osobną Azure AD app registration
   - Client secret dla OBO exchange

## 📝 **KOLEJNE KROKI**

1. **Weryfikacja konfiguracji Azure AD**
2. **Implementacja EmbeddedOboTokenManager**
3. **Naprawa ModernHttpService BaseAddress**
4. **Testowanie przepływu end-to-end**
5. **Dokumentacja nowej architektury**

---

**Data analizy**: 2025-01-21  
**Status**: Analiza zakończona, gotowe do implementacji  
**Priorytet**: WYSOKI - krytyczny dla funkcjonalności diagnostyki 