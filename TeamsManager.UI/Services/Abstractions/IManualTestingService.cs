using System.Threading.Tasks;
using TeamsManager.UI.Models;

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Interfejs dla serwisu testów manualnych
    /// </summary>
    public interface IManualTestingService
    {
        /// <summary>
        /// Tworzy domyślny zestaw testów
        /// </summary>
        TestSuite CreateDefaultTestSuite();

        /// <summary>
        /// Zapisuje wyniki testów do pliku markdown
        /// </summary>
        Task SaveTestResults(TestSuite testSuite);

        /// <summary>
        /// Ładuje zapisane wyniki testów
        /// </summary>
        Task<TestSuite?> LoadTestResults();

        /// <summary>
        /// Zapisuje konfigurację testów
        /// </summary>
        Task SaveTestConfig(TestSuite testSuite);

        /// <summary>
        /// Pobiera ścieżkę do pliku z wynikami
        /// </summary>
        string GetResultsFilePath();
    }
} 