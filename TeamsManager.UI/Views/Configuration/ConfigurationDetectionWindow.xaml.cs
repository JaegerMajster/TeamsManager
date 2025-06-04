using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.ViewModels.Configuration;

namespace TeamsManager.UI.Views.Configuration
{
    /// <summary>
    /// Logika interakcji dla klasy ConfigurationDetectionWindow.xaml
    /// </summary>
    public partial class ConfigurationDetectionWindow : Window
    {
        private ConfigurationDetectionViewModel? _viewModel;
        
        public ConfigurationDetectionWindow()
        {
            InitializeComponent();
        }
        
        public void SetValidationResult(ConfigurationValidationResult validationResult)
        {
            _viewModel = new ConfigurationDetectionViewModel(this, validationResult);
            DataContext = _viewModel;
        }
    }
}
