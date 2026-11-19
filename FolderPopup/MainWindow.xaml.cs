using System.Windows;
namespace FolderPopup;

public partial class MainWindow : Window
{
    readonly List<string> _folders = Config.Load();

    public MainWindow() { InitializeComponent(); Refresh(); }

    void Refresh() { FolderList.ItemsSource = null; FolderList.ItemsSource = _folders; }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFolderDialog();
        if (d.ShowDialog() == true && !_folders.Contains(d.FolderName))
        {
            _folders.Add(d.FolderName);
            Config.Save(_folders);
            Refresh();
        }
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is string p)
        {
            _folders.Remove(p);
            Config.Save(_folders);
            ShortcutHelper.Delete(p);
            ShortcutHelper.Notify();
            Refresh();
        }
    }

    void Apply_Click(object sender, RoutedEventArgs e)
    {
        foreach (var f in _folders) ShortcutHelper.Create(f, IconBuilder.Build(f));
        ShortcutHelper.Notify();
        MessageBox.Show("Готово, ярлыки на рабочем столе.");
    }
}
