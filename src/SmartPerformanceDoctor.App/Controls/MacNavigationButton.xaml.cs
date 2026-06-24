using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SmartPerformanceDoctor.App.Controls;

public sealed partial class MacNavigationButton : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MacNavigationButton), new PropertyMetadata("", OnChanged));

    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(MacNavigationButton), new PropertyMetadata("•", OnChanged));

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
    }
}
