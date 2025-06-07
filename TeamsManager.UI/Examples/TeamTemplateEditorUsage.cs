using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Core.Models;
using TeamsManager.UI.Views.Teams;
using TeamsManager.UI.ViewModels.Teams;

namespace TeamsManager.UI.Examples
{
    /// <summary>
    /// Przykłady użycia Team Templates Editor
    /// Pokazuje jak otwierać edytor dla nowych i istniejących szablonów
    /// </summary>
    public static class TeamTemplateEditorUsage
    {
        /// <summary>
        /// Otwiera edytor dla nowego szablonu
        /// </summary>
        /// <param name="serviceProvider">Provider serwisów DI</param>
        /// <returns>True jeśli szablon został zapisany</returns>
        public static async Task<bool> OpenNewTemplateEditorAsync(IServiceProvider serviceProvider)
        {
            // Pobranie okna i ViewModelu z DI
            var editorWindow = serviceProvider.GetRequiredService<TeamTemplateEditorWindow>();
            var viewModel = serviceProvider.GetRequiredService<TeamTemplateEditorViewModel>();
            
            // Ustawienie DataContext
            editorWindow.DataContext = viewModel;
            
            // Inicjalizacja dla nowego szablonu
            await editorWindow.InitializeForNewTemplateAsync();
            
            // Pokazanie okna jako dialog
            var result = editorWindow.ShowDialog();
            
            return result == true;
        }

        /// <summary>
        /// Otwiera edytor dla istniejącego szablonu
        /// </summary>
        /// <param name="serviceProvider">Provider serwisów DI</param>
        /// <param name="template">Szablon do edycji</param>
        /// <returns>True jeśli szablon został zaktualizowany</returns>
        public static async Task<bool> OpenEditTemplateEditorAsync(
            IServiceProvider serviceProvider, 
            TeamTemplate template)
        {
            // Pobranie okna i ViewModelu z DI
            var editorWindow = serviceProvider.GetRequiredService<TeamTemplateEditorWindow>();
            var viewModel = serviceProvider.GetRequiredService<TeamTemplateEditorViewModel>();
            
            // Ustawienie DataContext
            editorWindow.DataContext = viewModel;
            
            // Inicjalizacja dla edycji szablonu
            await editorWindow.InitializeForEditAsync(template);
            
            // Pokazanie okna jako dialog
            var result = editorWindow.ShowDialog();
            
            return result == true;
        }

        /// <summary>
        /// Przykład tworzenia szablonu programowo
        /// </summary>
        /// <returns>Przykładowy szablon</returns>
        public static TeamTemplate CreateExampleTemplate()
        {
            return new TeamTemplate
            {
                Name = "Szablon Edukacyjny",
                Template = "{TypSzkoly} {Oddzial} - {Przedmiot} - {Nauczyciel}",
                Description = "Standardowy szablon dla zespołów edukacyjnych",
                Category = "Edukacyjny",
                IsUniversal = true,
                Language = "Polski",
                Separator = " - ",
                MaxLength = 100,
                RemovePolishChars = false
            };
        }

        /// <summary>
        /// Przykład testowania szablonu z danymi
        /// </summary>
        /// <param name="template">Szablon do testowania</param>
        /// <returns>Wygenerowana nazwa</returns>
        public static string TestTemplateWithData(TeamTemplate template)
        {
            var testData = new Dictionary<string, string>
            {
                { "TypSzkoly", "LO" },
                { "Oddzial", "1A" },
                { "Przedmiot", "Matematyka" },
                { "Nauczyciel", "Jan Kowalski" }
            };

            return template.GenerateTeamName(testData);
        }
    }
} 