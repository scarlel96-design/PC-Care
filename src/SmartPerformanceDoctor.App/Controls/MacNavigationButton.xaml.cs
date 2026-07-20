using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SmartPerformanceDoctor.App.Controls;

public sealed partial class MacNavigationButton : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MacNavigationButton), new PropertyMetadata("", OnChanged));

    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(MacNavigationButton), new PropertyMetadata("•", OnChanged));
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(MacNavigationButton), new PropertyMetadata(false, OnChanged));

    public event RoutedEventHandler? Click;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public MacNavigationButton()
    {
        InitializeComponent();
        RootButton.Click += (_, e) => Click?.Invoke(this, e);
        Apply();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MacNavigationButton button)
        {
            button.Apply();
        }
    }

    private void Apply()
    {
        SymbolText.Text = Symbol;
        TitleText.Text = Title;
        RootButton.Background = IsSelected
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PccAccentSoftBrush"]
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var foreground = IsSelected
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PccAccentBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PccTextBrush"];
        SymbolText.Foreground = foreground;
        TitleText.Foreground = foreground;
    }
}
