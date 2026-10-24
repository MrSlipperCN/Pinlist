using System.Windows;

namespace Pinlist.Desktop;

public partial class LanguageSelectionWindow : Window
{
    public string SelectedLanguage { get; private set; } = UiText.DetectLanguage();

    public LanguageSelectionWindow()
    {
        InitializeComponent();
    }

    private void English_Click(object sender, RoutedEventArgs e)
    {
        SelectedLanguage = UiText.English;
        DialogResult = true;
    }

    private void Chinese_Click(object sender, RoutedEventArgs e)
    {
        SelectedLanguage = UiText.SimplifiedChinese;
        DialogResult = true;
    }
}
