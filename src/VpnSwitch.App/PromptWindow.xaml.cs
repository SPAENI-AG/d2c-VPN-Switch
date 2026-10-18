using System.Windows;

namespace VpnSwitch.App;

/// <summary>Einfacher Eingabedialog (ersetzt den Eingabedialog der WinForms-Version).</summary>
public partial class PromptWindow : Window
{
    private PromptWindow(string title, string text, string value)
    {
        InitializeComponent();
        Title = title;
        tb_Text.Text = text;
        tb_Input.Text = value;
        tb_Input.SelectAll();
    }

    /// <summary>Gibt den eingegebenen Text zurück, bei Abbruch null.</summary>
    public static string? Ask(Window owner, string title, string text, string value)
    {
        var w = new PromptWindow(title, text, value) { Owner = owner };
        return w.ShowDialog() == true ? w.tb_Input.Text : null;
    }

    private void bt_Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
