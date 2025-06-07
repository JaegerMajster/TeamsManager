using System;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.UI.ViewModels.Subjects;

namespace TeamsManager.UI.Views.Subjects
{
    /// <summary>
    /// Interaction logic for SubjectsView.xaml
    /// </summary>
    public partial class SubjectsView : UserControl
    {
        public SubjectsView()
        {
            InitializeComponent();
            
            // Get ViewModel from DI
            DataContext = App.ServiceProvider.GetRequiredService<SubjectsViewModel>();
            
            Loaded += async (s, e) =>
            {
                if (DataContext is SubjectsViewModel vm)
                {
                    await vm.InitializeAsync();
                }
            };
        }
    }
} 