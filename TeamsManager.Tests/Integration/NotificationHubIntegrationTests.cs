using Xunit;
using FluentAssertions;

namespace TeamsManager.Tests.Integration
{
    /// <summary>
    /// Podstawowe testy weryfikujące konfigurację SignalR
    /// Pełne testy integracyjne wymagają dostępu do Program class
    /// </summary>
    public class NotificationHubIntegrationTests
    {

        [Fact]
        public async Task SignalRHub_ShouldAcceptConnectionWithValidJwtToken()
        {
            // Arrange
            var notificationReceived = false;
            var tcs = new TaskCompletionSource<bool>();

            _hubConnection.On<object>("ReceiveNotification", notification =>
            {
                notificationReceived = true;
                tcs.SetResult(true);
            });

            // Act
            await _hubConnection.StartAsync();
            
            // Assert
            _hubConnection.State.Should().Be(HubConnectionState.Connected);
            
            // Czekaj na powiadomienie powitalne (lub timeout)
            var received = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;
            
            // Sprawdź że połączenie się udało (nawet jeśli nie ma powiadomienia powitalnego)
            _hubConnection.State.Should().Be(HubConnectionState.Connected);
        }

        [Fact]
        public async Task SignalRHub_ShouldRejectConnectionWithoutToken()
        {
            // Arrange
            var unauthorizedConnection = new HubConnectionBuilder()
                .WithUrl($"{_server.BaseAddress}notificationHub", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                    // Brak tokenu!
                })
                .Build();

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await unauthorizedConnection.StartAsync();
            });
        }

        [Fact]
        public async Task SignalRHub_ShouldHandleTokenFromQueryString()
        {
            // Arrange
            var queryStringConnection = new HubConnectionBuilder()
                .WithUrl($"{_server.BaseAddress}notificationHub?access_token={_testToken}", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                    // Token w query string zamiast w nagłówku
                })
                .Build();

            // Act
            await queryStringConnection.StartAsync();

            // Assert
            queryStringConnection.State.Should().Be(HubConnectionState.Connected);
            await queryStringConnection.DisposeAsync();
        }

        private string GenerateTestJwtToken()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-for-integration-tests-must-be-long-enough"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim("upn", "test@example.com"),
                new Claim(ClaimTypes.Role, "Administrator")
            };

            var token = new JwtSecurityToken(
                issuer: "test-issuer",
                audience: "test-audience",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
            _httpClient?.Dispose();
            _server?.Dispose();
        }
    }
} 