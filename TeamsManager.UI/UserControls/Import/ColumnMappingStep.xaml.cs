using System.Windows.Controls;
using TeamsManager.UI.ViewModels.Import;

namespace TeamsManager.UI.UserControls.Import
{
    /// <summary>
    /// Interaction logic for ColumnMappingStep.xaml
    /// </summary>
    public partial class ColumnMappingStep : UserControl
    {
        public ColumnMappingStep()
        {
            InitializeComponent();
        }

        private void UserControl_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ImportColumnMappingViewModel viewModel)
            {
                // Dynamiczne generowanie kolumn dla DataGrid podglądu
                GeneratePreviewColumns(viewModel);
            }
        }

        private void GeneratePreviewColumns(ImportColumnMappingViewModel viewModel)
        {
            var dataGrid = FindName("PreviewDataGrid") as DataGrid;
            if (dataGrid == null) return;

            dataGrid.Columns.Clear();

            foreach (var mapping in viewModel.ColumnMappings)
            {
                if (mapping.IsMapped && !string.IsNullOrEmpty(mapping.TargetField))
                {
                    var column = new DataGridTextColumn
                    {
                        Header = $"{mapping.TargetField} ← {mapping.SourceColumn}",
                        Binding = new System.Windows.Data.Binding($"[{mapping.SourceColumn}]"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    };
                    dataGrid.Columns.Add(column);
                }
            }
        }
    }
} 