// Plik: TeamsManager.Api/Hubs/NotificationHub.cs
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
// Dodaj using Microsoft.AspNetCore.Authorization; jeśli zdecydujesz się na zabezpieczenie huba
// Dodaj using Microsoft.Extensions.Logging; jeśli chcesz logować zdarzenia w hubie

namespace TeamsManager.Api.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        // Konstruktor z logowaniem (opcjonalny, ale dobry zwyczaj)
        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        // Przykład metody, którą klient mógłby wywołać na serwerze (jeśli potrzebne)
        // Na razie hub będzie głównie używany do wysyłania wiadomości z serwera do klienta.
        // public async Task SendMessage(string user, string message)
        // {
        //     await Clients.All.SendAsync("ReceiveMessage", user, message);
        // }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("SignalR Hub: Nowe połączenie. ConnectionId: {ConnectionId}", Context.ConnectionId);
            // Tutaj możesz dodać logikę dla [Authorize] (punkt 4 z Twojego planu)
            // np. pobranie Context.UserIdentifier i dodanie do grupy
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogError(exception, "SignalR Hub: Rozłączenie z błędem. ConnectionId: {ConnectionId}", Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("SignalR Hub: Rozłączenie. ConnectionId: {ConnectionId}", Context.ConnectionId);
            }
            // Tutaj możesz dodać logikę usuwania z grupy, jeśli była używana
            await base.OnDisconnectedAsync(exception);
        }
    }
}