### Plan Szczegółowy dla Kroku 3.2.5.1: Przygotowanie Testowego Endpointu w `TeamsManager.Api`

* [ ] **Utworzenie lub Modyfikacja Kontrolera:**
    * [ ] Utwórz nowy kontroler `TestAuthController.cs` w folderze `TeamsManager.Api/Controllers/` LUB zdecyduj się na modyfikację istniejącego `WeatherForecastController.cs`.
        * *Decyzja Rekomendowana:* Jeśli `WeatherForecastController` jest tylko placeholderem, lepiej stworzyć nowy, dedykowany kontroler `TestAuthController`.
* [ ] **Implementacja Endpointu `GET /api/TestAuth/whoami`:**
    * [ ] W kontrolerze zdefiniuj metodę publiczną np. `WhoAmI()` zwracającą `IActionResult`.
    * [ ] Oznacz metodę atrybutem `[HttpGet("whoami")]`.
    * [ ] **Oznacz metodę atrybutem `[Authorize]`**, aby wymusić uwierzytelnianie.
    * [ ] Wstrzyknij do konstruktora kontrolera `ICurrentUserService` oraz `ILogger<NazwaTwojegoKontrolera>` (np. `ILogger<TestAuthController>`).
    * [ ] Wewnątrz metody `WhoAmI()`:
        * [ ] Pobierz UPN zalogowanego użytkownika za pomocą `_currentUserService.GetCurrentUserUpn()`.
        * [ ] (Opcjonalnie) Pobierz ID obiektu użytkownika (ObjectId) za pomocą `_currentUserService.GetCurrentUserId()` (jeśli zaimplementowałeś tę metodę w `CurrentUserService`).
        * [ ] Zaloguj (używając wstrzykniętego `_logger`) pobrane informacje (UPN, ObjectId) lub ewentualny błąd (np. gdy UPN jest pusty mimo uwierzytelnienia).
        * [ ] Zwróć `OkObjectResult` zawierający obiekt anonimowy lub dedykowane DTO z pobranym UPN i ObjectId, np. `return Ok(new { UserPrincipalName = userUpn, ObjectId = userId });`.
        * [ ] Dodaj obsługę sytuacji, gdy `userUpn` jest `null` lub pusty po uwierzytelnieniu (mimo że użytkownik jest `HttpContext.User.Identity.IsAuthenticated == true`). W takim przypadku metoda powinna zwrócić np. `StatusCode(500, "Nie udało się pobrać UPN zalogowanego użytkownika z serwisu.")`.
* [ ] **Implementacja Publicznego Endpointu (dla kontrastu i testowania działania API bez autoryzacji):**
    * [ ] W tym samym kontrolerze (`TestAuthController`) dodaj drugą metodę, np. `PublicInfo()`.
    * [ ] Oznacz ją atrybutem `[HttpGet("publicinfo")]`.
    * [ ] **NIE dodawaj** do niej atrybutu `[Authorize]`.
    * [ ] Zwróć prostą odpowiedź, np. `return Ok(new { Message = "To jest publiczny endpoint, dostępny bez logowania." });`.
* [ ] **Weryfikacja Kompilacji API:**
    * [ ] Upewnij się, że projekt `TeamsManager.Api` kompiluje się bez błędów po dodaniu nowego kontrolera i jego logiki.