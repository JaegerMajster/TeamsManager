using UserControl = System.Windows.Controls.UserControl;

namespace TeamsManager.UI.UserControls.Settings
{
    /// <summary>
    /// Kontrolka do dynamicznego edytowania wartości ustawień aplikacji
    /// Automatycznie dostosowuje interfejs do typu danych
    /// </summary>
    public partial class SettingEditorControl : UserControl
    {
        public SettingEditorControl()
        {
            InitializeComponent();
        }
    }
} 