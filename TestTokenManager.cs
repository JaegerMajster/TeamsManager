using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Services;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== TEST TokenManager ===");
        
        try
        {
            // Konfiguracja DI
            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddLogging(builder => builder.AddConsole());
            
            // Mock konfiguracji
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "test-tenant",
                ["AzureAd:ClientId"] = "test-client",
                ["AzureAd:ClientSecret"] = "test-secret"
            });
            var configuration = configBuilder.Build();
            services.AddSingleton<IConfiguration>(configuration);
            
            // Mock IConfidentialClientApplication
            services.AddScoped<IConfidentialClientApplication>(provider =>
            {
                // To będzie mock - nie będzie działać, ale sprawdzi czy DI działa
                return ConfidentialClientApplicationBuilder.Create("test-client")
                    .WithClientSecret("test-secret")
                    .WithAuthority(new Uri("https://login.microsoftonline.com/test-tenant"))
                    .Build();
            });
            
            // Rejestracja TokenManager
            services.AddScoped<ITokenManager, TokenManager>();
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Test 1: Sprawdź czy TokenManager jest zarejestrowany
            Console.WriteLine("Test 1: Sprawdzam rejestrację TokenManager...");
            var tokenManager = serviceProvider.GetService<ITokenManager>();
            
            if (tokenManager != null)
            {
                Console.WriteLine("✅ ITokenManager - ZAREJESTROWANY");
                Console.WriteLine($"Typ implementacji: {tokenManager.GetType().FullName}");
                
                // Test 2: Sprawdź podstawowe metody
                Console.WriteLine("\nTest 2: Sprawdzam podstawowe metody...");
                
                // Test HasValidToken
                var hasToken = tokenManager.HasValidToken("test@example.com");
                Console.WriteLine($"✅ HasValidToken('test@example.com') = {hasToken}");
                
                // Test ClearUserTokens
                tokenManager.ClearUserTokens("test@example.com");
                Console.WriteLine("✅ ClearUserTokens('test@example.com') - wykonano");
                
                Console.WriteLine("\n✅ WSZYSTKIE TESTY PODSTAWOWE PRZESZŁY POMYŚLNIE");
            }
            else
            {
                Console.WriteLine("❌ ITokenManager - NIE ZAREJESTROWANY");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ BŁĄD: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\n=== KONIEC TESTÓW ===");
    }
} 