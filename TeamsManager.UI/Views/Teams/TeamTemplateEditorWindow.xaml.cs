using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels.Teams;

namespace TeamsManager.UI.Views.Teams
{
    /// <summary>
    /// Okno edytora szablonów zespołów z live preview i pomocnikiem tokenów
    /// </summary>
    public partial class TeamTemplateEditorWindow : Window
    {
        private readonly TeamTemplateEditorViewModel _viewModel;

        /// <summary>
        /// Konstruktor okna z wstrzykniętym ViewModelem
        /// </summary>
        public TeamTemplateEditorWindow(TeamTemplateEditorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            
            // Obsługa zamykania okna
            _viewModel.RequestClose += OnRequestClose;
        }

        /// <summary>
        /// Inicjalizuje okno dla nowego szablonu
        /// </summary>
        public async Task InitializeForNewTemplateAsync()
        {
            await _viewModel.InitializeAsync();
        }

        /// <summary>
        /// Inicjalizuje okno dla edycji istniejącego szablonu
        /// </summary>
        /// <param name="template">Szablon do edycji</param>
        public async Task InitializeForEditAsync(TeamTemplate template)
        {
            _viewModel.Template = template;
            await _viewModel.InitializeAsync();
        }

        /// <summary>
        /// Obsługuje żądanie zamknięcia okna z ViewModelu
        /// </summary>
        private void OnRequestClose()
        {
            DialogResult = _viewModel.DialogResult;
            Close();
        }

        /// <summary>
        /// Cleanup przy zamykaniu okna
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            _viewModel.RequestClose -= OnRequestClose;
            base.OnClosing(e);
        }
    }
} 