using System;
using System.Windows.Input;
using TeamsManager.UI.Models;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel dla uniwersalnego okna dialogowego
    /// </summary>
    public class UniversalDialogViewModel : BaseViewModel
    {
        private DialogOptions _options;
        private DateTime _startTime;

        public UniversalDialogViewModel()
        {
            _options = new DialogOptions();
            _startTime = DateTime.Now;

            PrimaryCommand = new RelayCommand(OnPrimaryClick);
            SecondaryCommand = new RelayCommand(OnSecondaryClick);
            CloseCommand = new RelayCommand(OnClose);
        }

        #region Properties

        /// <summary>
        /// Opcje dialogu
        /// </summary>
        public DialogOptions Options
        {
            get => _options;
            set
            {
                if (SetProperty(ref _options, value))
                {
                    UpdateProperties();
                }
            }
        }

        /// <summary>
        /// Tytuł okna
        /// </summary>
        public string Title => Options?.Title ?? "Dialog";

        /// <summary>
        /// Główna treść komunikatu
        /// </summary>
        public string Message => Options?.Message ?? "";

        /// <summary>
        /// Dodatkowe szczegóły
        /// </summary>
        public string? Details => Options?.Details;

        /// <summary>
        /// Czy pokazać szczegóły
        /// </summary>
        public bool HasDetails => !string.IsNullOrWhiteSpace(Options?.Details);

        /// <summary>
        /// Tekst głównego przycisku
        /// </summary>
        public string PrimaryButtonText => GetPrimaryButtonText();

        /// <summary>
        /// Tekst drugiego przycisku
        /// </summary>
        public string SecondaryButtonText => GetSecondaryButtonText();

        /// <summary>
        /// Czy pokazać drugi przycisk
        /// </summary>
        public bool ShowSecondaryButton => Options?.ShowSecondaryButton ?? false;

        /// <summary>
        /// Ikona dla typu dialogu
        /// </summary>
        public string IconKind => GetIconKind();

        /// <summary>
        /// Kolor ikony
        /// </summary>
        public string IconColor => GetIconColor();

        /// <summary>
        /// Kolor tła ikony
        /// </summary>
        public string IconBackground => GetIconBackground();

        /// <summary>
        /// Maksymalna szerokość okna - dynamicznie dostosowana do zawartości
        /// </summary>
        public double MaxWidth => CalculateOptimalMaxWidth();

        /// <summary>
        /// Czy dialog ma długi tekst wymagający większej szerokości
        /// </summary>
        public bool HasLongContent => !string.IsNullOrEmpty(Message) && Message.Length > 100 || 
                                     !string.IsNullOrEmpty(Details) && Details.Length > 200;

        /// <summary>
        /// Czy główny przycisk jest domyślny
        /// </summary>
        public bool IsPrimaryDefault => Options?.IsPrimaryDefault ?? true;

        /// <summary>
        /// Czy drugi przycisk jest przyciskiem anulowania
        /// </summary>
        public bool IsSecondaryCancel => Options?.IsSecondaryCancel ?? true;

        #endregion

        #region Commands

        public ICommand PrimaryCommand { get; }
        public ICommand SecondaryCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        #region Events

        /// <summary>
        /// Zdarzenie zamknięcia okna z wynikiem
        /// </summary>
        public event Action<DialogResponse>? DialogClosed;

        #endregion

        #region Public Methods

        /// <summary>
        /// Inicjalizuje dialog z opcjami
        /// </summary>
        public void Initialize(DialogOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _startTime = DateTime.Now;
        }

        #endregion

        #region Private Methods

        private void UpdateProperties()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(HasDetails));
            OnPropertyChanged(nameof(HasLongContent));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
            OnPropertyChanged(nameof(ShowSecondaryButton));
            OnPropertyChanged(nameof(IconKind));
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(IconBackground));
            OnPropertyChanged(nameof(MaxWidth));
            OnPropertyChanged(nameof(IsPrimaryDefault));
            OnPropertyChanged(nameof(IsSecondaryCancel));
        }

        /// <summary>
        /// Oblicza optymalną maksymalną szerokość okna na podstawie zawartości
        /// </summary>
        private double CalculateOptimalMaxWidth()
        {
            // Jeśli użytkownik ustawił konkretną szerokość, użyj jej
            if (Options?.MaxWidth.HasValue == true)
                return Options.MaxWidth.Value;

            // Bazowa szerokość - optymalna dla większości dialogów
            double baseWidth = 480;

            // Zwiększ szerokość dla długich tytułów
            if (!string.IsNullOrEmpty(Title) && Title.Length > 25)
                baseWidth += Math.Min((Title.Length - 25) * 8, 200);

            // Zwiększ szerokość dla długich komunikatów
            if (!string.IsNullOrEmpty(Message))
            {
                if (Message.Length > 120)
                    baseWidth += Math.Min((Message.Length - 120) * 3, 250);
                else if (Message.Length > 60)
                    baseWidth += Math.Min((Message.Length - 60) * 2, 120);
            }

            // Zwiększ szerokość dla szczegółów - ale nie za bardzo, bo mają się zawijać
            if (!string.IsNullOrEmpty(Details))
            {
                if (Details.Length > 200)
                    baseWidth += Math.Min((Details.Length - 200) * 1.5, 150);
                else if (Details.Length > 100)
                    baseWidth += Math.Min((Details.Length - 100) * 1, 100);
            }

            // Zwiększ szerokość dla długich tekstów przycisków
            var primaryLength = PrimaryButtonText?.Length ?? 0;
            var secondaryLength = SecondaryButtonText?.Length ?? 0;
            var maxButtonLength = Math.Max(primaryLength, secondaryLength);
            
            if (maxButtonLength > 12)
                baseWidth += Math.Min((maxButtonLength - 12) * 6, 80);

            // Ogranicz do rozsądnych wartości - szerokość nie powinna być za duża
            return Math.Min(Math.Max(baseWidth, 420), 800);
        }

        private string GetPrimaryButtonText()
        {
            if (!string.IsNullOrEmpty(Options?.PrimaryButtonText))
                return Options.PrimaryButtonText;

            return (Options?.Type ?? DialogType.Information) switch
            {
                DialogType.Information => "OK",
                DialogType.Warning => "OK",
                DialogType.Error => "OK",
                DialogType.Success => "OK",
                DialogType.Confirmation => "Tak",
                DialogType.Question => "OK",
                _ => "OK"
            };
        }

        private string GetSecondaryButtonText()
        {
            if (!string.IsNullOrEmpty(Options?.SecondaryButtonText))
                return Options.SecondaryButtonText;

            return (Options?.Type ?? DialogType.Information) switch
            {
                DialogType.Confirmation => "Nie",
                DialogType.Question => "Anuluj",
                _ => "Anuluj"
            };
        }

        private string GetIconKind()
        {
            return (Options?.Type ?? DialogType.Information) switch
            {
                DialogType.Information => "Information",
                DialogType.Warning => "Alert",
                DialogType.Error => "AlertCircle",
                DialogType.Success => "CheckCircle",
                DialogType.Confirmation => "HelpCircle",
                DialogType.Question => "HelpCircle",
                _ => "Information"
            };
        }

        private string GetIconColor()
        {
            return (Options?.Type ?? DialogType.Information) switch
            {
                DialogType.Information => "White",
                DialogType.Warning => "White",
                DialogType.Error => "White",
                DialogType.Success => "White",
                DialogType.Confirmation => "White",
                DialogType.Question => "White",
                _ => "White"
            };
        }

        private string GetIconBackground()
        {
            return (Options?.Type ?? DialogType.Information) switch
            {
                DialogType.Information => "{DynamicResource AccentBlue}",
                DialogType.Warning => "{DynamicResource AccentYellow}",
                DialogType.Error => "{DynamicResource AccentRed}",
                DialogType.Success => "{DynamicResource AccentGreen}",
                DialogType.Confirmation => "{DynamicResource AccentBlue}",
                DialogType.Question => "{DynamicResource AccentBlue}",
                _ => "{DynamicResource AccentBlue}"
            };
        }

        private void OnPrimaryClick()
        {
            var result = (Options?.Type ?? DialogType.Information) switch
            {
                DialogType.Confirmation => DialogResult.Yes,
                DialogType.Question => DialogResult.Primary,
                _ => DialogResult.OK
            };

            CloseWithResult(result);
        }

        private void OnSecondaryClick()
        {
            var result = (Options?.Type ?? DialogType.Information) switch
            {
                DialogType.Confirmation => DialogResult.No,
                DialogType.Question => DialogResult.Secondary,
                _ => DialogResult.Cancel
            };

            CloseWithResult(result);
        }

        private void OnClose()
        {
            CloseWithResult(DialogResult.Cancel);
        }

        private void CloseWithResult(DialogResult result)
        {
            var response = new DialogResponse
            {
                Result = result,
                DisplayTime = DateTime.Now - _startTime,
                Tag = Options
            };

            DialogClosed?.Invoke(response);
        }

        #endregion
    }
} 