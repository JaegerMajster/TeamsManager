# Instrukcja Stosowania Wspólnych Stylów WPF w TeamsManager.UI - v2.0

## 🎨 Wprowadzenie

Ten dokument opisuje, jak używać nowoczesnych stylów zdefiniowanych w `Styles/CommonStyles.xaml` w aplikacji TeamsManager.UI. Style zostały zaprojektowane dla Material Design 5.2.1 w .NET 8.0, oferując nowoczesny, animowany interfejs z efektami głębi i gradientami.

### Kluczowe cechy designu:
- **Ciemny motyw z gradientami:** Subtelne gradienty i dekoracyjne elementy
- **Efekty głębi:** Trzy poziomy cieni (ShadowLight, ShadowMedium, ShadowHeavy)
- **Animacje i interakcje:** Płynne przejścia, hover effects, animacje wejścia
- **Zaokrąglone elementy:** Corner radius 8-12px dla nowoczesnego wyglądu
- **Akcenty kolorystyczne:** Blue jako primary, Lime jako secondary/success

## 🚀 Konfiguracja

### 1. Konfiguracja App.xaml:
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <!-- Material Design 5.2.1 BundledTheme -->
            <materialDesign:BundledTheme BaseTheme="Dark" PrimaryColor="Blue" SecondaryColor="Lime" />
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml" />
            
            <!-- Dodatkowe kolory -->
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignColors;component/Themes/MaterialDesignColor.Green.xaml" />
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignColors;component/Themes/MaterialDesignColor.Red.xaml" />
            <ResourceDictionary Source="pack://application:,,,/MaterialDesignColors;component/Themes/MaterialDesignColor.Orange.xaml" />
            
            <!-- Własne style -->
            <ResourceDictionary Source="Styles/CommonStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        
        <!-- Globalne kolory -->
        <SolidColorBrush x:Key="BackgroundDark" Color="#FF202020"/>
        <SolidColorBrush x:Key="BackgroundMedium" Color="#FF252526"/>
        <SolidColorBrush x:Key="BackgroundLight" Color="#FF2D2D30"/>
        <SolidColorBrush x:Key="BorderDark" Color="#FF3F3F46"/>
        <SolidColorBrush x:Key="TextPrimary" Color="#FFECECEC"/>
        <SolidColorBrush x:Key="TextSecondary" Color="#FF969696"/>
        <SolidColorBrush x:Key="AccentBlue" Color="#FF0078D4"/>
        <SolidColorBrush x:Key="AccentRed" Color="#FFD13438"/>
    </ResourceDictionary>
</Application.Resources>
```

### 2. Namespace w XAML:
```xml
<Window xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
        Style="{StaticResource BaseWindowStyle}">
```

## 📦 Kluczowe Style i Komponenty

### 🎯 Przyciski z Animacjami

#### **PrimaryActionButton** - Główne akcje
```xml
<Button Style="{StaticResource PrimaryActionButton}" 
        Width="280" Height="48">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Save" Margin="0,0,8,0"/>
        <TextBlock Text="Zapisz" VerticalAlignment="Center"/>
    </StackPanel>
</Button>
```
- Gradient background (HeaderGradient)
- Scale animation on hover (1.02x)
- Efekt cienia z dynamiczną zmianą

#### **SecondaryActionButton** - Akcje drugorzędne
```xml
<Button Style="{StaticResource SecondaryActionButton}" Content="Anuluj"/>
```
- Przezroczyste tło z obramowaniem
- Subtelny hover effect

#### **DangerButton** - Akcje niebezpieczne
```xml
<Button Style="{StaticResource DangerButton}">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Delete" Margin="0,0,8,0"/>
        <TextBlock Text="Usuń"/>
    </StackPanel>
</Button>
```

#### **FloatingActionButton** - FAB
```xml
<Button Style="{StaticResource FloatingActionButton}">
    <materialDesign:PackIcon Kind="Plus" Width="24" Height="24"/>
</Button>
```
- Lime background
- Okrągły kształt
- Heavy shadow on hover

#### **IconButton** - Małe przyciski z ikonami
```xml
<Button Style="{StaticResource IconButton}" ToolTip="Ustawienia">
    <materialDesign:PackIcon Kind="Cog" Width="20" Height="20"/>
</Button>
```

### 📝 Style Tekstowe

```xml
<TextBlock Text="Nagłówek Strony" Style="{StaticResource PageTitleStyle}"/>
<TextBlock Text="Sekcja" Style="{StaticResource SectionHeaderStyle}"/>
<TextBlock Text="Instrukcje..." Style="{StaticResource InstructionTextStyle}"/>
<TextBlock Text="Błąd!" Style="{StaticResource ErrorTextStyle}"/>
```

### 📊 Kontrolki Formularzy z Zaokrągleniami

#### **TextBox z zaokrąglonymi rogami**
```xml
<Border Style="{StaticResource RoundedTextBox}">
    <TextBox materialDesign:HintAssist.Hint="Nazwa użytkownika" 
             BorderThickness="0"/>
</Border>
```

#### **Standardowy TextBox/PasswordBox**
```xml
<TextBox materialDesign:HintAssist.Hint="Email" 
         materialDesign:HintAssist.IsFloating="True"/>
<PasswordBox materialDesign:HintAssist.Hint="Hasło" 
             materialDesign:HintAssist.IsFloating="True"/>
```

### 📋 DataGrid z Gradientowym Nagłówkiem

```xml
<Border Style="{StaticResource RoundedDataGrid}">
    <DataGrid ItemsSource="{Binding Items}" AutoGenerateColumns="False">
        <DataGrid.Columns>
            <DataGridTextColumn Header="Nazwa" Binding="{Binding Name}" Width="*"/>
            <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="100"/>
        </DataGrid.Columns>
    </DataGrid>
</Border>
```
- Gradient w nagłówkach
- Animacja fade-in dla wierszy
- Zaokrąglone rogi przez wrapper

### 🎴 Karty Informacyjne

```xml
<Border Style="{StaticResource InfoCardStyle}">
    <StackPanel>
        <TextBlock Text="Tytuł Karty" Style="{StaticResource SectionHeaderStyle}"/>
        <TextBlock Text="Treść karty..." Style="{StaticResource InstructionTextStyle}"/>
    </StackPanel>
</Border>
```
- Efekt unoszenia przy hover
- Corner radius 12px
- Dynamiczny cień

### 📊 Inne Komponenty

#### **ProgressBar z gradientem**
```xml
<ProgressBar Value="65" Style="{StaticResource MaterialDesignLinearProgressBar}"/>
```

#### **CheckBox z animacją**
```xml
<CheckBox Content="Zapamiętaj mnie" IsChecked="{Binding RememberMe}"/>
```
- Zmiana koloru na Lime przy zaznaczeniu
- Animacja scale przy toggle

#### **Status Indicator**
```xml
<Ellipse Style="{StaticResource StatusIndicator}" Fill="{StaticResource SuccessGreen}"/>
```
- Pulsująca animacja

#### **Chip/Tag**
```xml
<Border Style="{StaticResource ChipStyle}">
    <TextBlock Text="Nowy" Foreground="White" FontSize="12"/>
</Border>
```

## 🎬 Animacje

### Dostępne Storyboardy:
```xml
<!-- Fade In -->
<Border.Triggers>
    <EventTrigger RoutedEvent="Loaded">
        <BeginStoryboard Storyboard="{StaticResource FadeIn}"/>
    </EventTrigger>
</Border.Triggers>

<!-- Slide od lewej -->
<BeginStoryboard Storyboard="{StaticResource SlideInFromLeft}"/>

<!-- Slide od dołu -->
<BeginStoryboard Storyboard="{StaticResource SlideInFromBottom}"/>
```

### Loading Overlay:
```xml
<Grid x:Name="LoadingOverlay" Background="{StaticResource BackgroundDark}" 
      Opacity="0.9" Visibility="Collapsed">
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
        <ProgressBar Style="{StaticResource MaterialDesignCircularProgressBar}"
                     IsIndeterminate="True" Width="50" Height="50"/>
        <TextBlock Text="Ładowanie..." Margin="0,16,0,0"/>
    </StackPanel>
</Grid>
```

## 🎨 Efekty i Gradienty

### Używanie cieni:
```xml
<Border Effect="{StaticResource ShadowLight}">
    <!-- Lekki cień -->
</Border>

<Border Effect="{StaticResource ShadowMedium}">
    <!-- Średni cień -->
</Border>

<Border Effect="{StaticResource ShadowHeavy}">
    <!-- Mocny cień -->
</Border>
```

### Gradienty:
```xml
<!-- Header Gradient (Blue) -->
<Border Background="{StaticResource HeaderGradient}"/>

<!-- Accent Gradient (Blue->Lime) -->
<Border Background="{StaticResource AccentGradient}"/>
```

## 💡 Najlepsze Praktyki

### 1. **Struktura okna z dekoracyjnymi elementami:**
```xml
<Grid>
    <!-- Gradient Background -->
    <Grid.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
            <GradientStop Color="#FF202020" Offset="0"/>
            <GradientStop Color="#FF252526" Offset="0.5"/>
            <GradientStop Color="#FF2D2D30" Offset="1"/>
        </LinearGradientBrush>
    </Grid.Background>
    
    <!-- Dekoracyjne koła -->
    <Canvas>
        <Ellipse Width="200" Height="200" Canvas.Left="-100" Canvas.Top="-50" Opacity="0.05">
            <Ellipse.Fill>
                <RadialGradientBrush>
                    <GradientStop Color="#FF0078D4" Offset="0"/>
                    <GradientStop Color="Transparent" Offset="1"/>
                </RadialGradientBrush>
            </Ellipse.Fill>
        </Ellipse>
    </Canvas>
    
    <!-- Główna zawartość -->
    <Border Style="{StaticResource InfoCardStyle}" Margin="20">
        <!-- Content -->
    </Border>
</Grid>
```

### 2. **Kombinowanie ikon Material Design:**
```xml
<StackPanel Orientation="Horizontal">
    <materialDesign:PackIcon Kind="Account" VerticalAlignment="Center" Margin="0,0,8,0"/>
    <TextBlock Text="Profil użytkownika" VerticalAlignment="Center"/>
</StackPanel>
```

### 3. **Responsywne layouty:**
```xml
<Grid MaxWidth="800" Margin="20">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <!-- Content -->
</Grid>
```

### 4. **Używanie DialogHost dla dialogów:**
```xml
<materialDesign:DialogHost DialogTheme="Inherit">
    <!-- Main content -->
</materialDesign:DialogHost>
```

## ⚠️ Uwagi dla Material Design 5.2.1

- **Brak CornerRadius** w assist properties - używaj custom templates lub Border wrappers
- **BundledTheme** zamiast osobnych plików kolorów
- **TextBoxViewMargin** zamiast CornerRadius dla TextBox
- Preferuj **Material Design ikony** zamiast emoji

## 🔧 Rozszerzanie Stylów

### Tworzenie własnych stylów bazujących na istniejących:
```xml
<Style x:Key="MyCustomButton" TargetType="Button" 
       BasedOn="{StaticResource PrimaryActionButton}">
    <Setter Property="MinWidth" Value="200"/>
    <Setter Property="FontSize" Value="16"/>
</Style>
```

### Lokalne modyfikacje:
```xml
<Button Style="{StaticResource PrimaryActionButton}">
    <Button.Resources>
        <SolidColorBrush x:Key="AccentBlue" Color="#FF00AA00"/>
    </Button.Resources>
    <Button.Content>Zielony przycisk</Button.Content>
</Button>
```

## 📚 Podsumowanie

Style w CommonStyles.xaml oferują:
- ✅ Nowoczesny design z gradientami i cieniami
- ✅ Płynne animacje i interakcje
- ✅ Pełną kompatybilność z Material Design 5.2.1
- ✅ Responsywność i elastyczność
- ✅ Łatwość rozszerzania i modyfikacji

Pamiętaj o używaniu `materialDesign:PackIcon` dla ikon oraz o strukturze z gradient background i dekoracyjnymi elementami dla najlepszego efektu wizualnego!