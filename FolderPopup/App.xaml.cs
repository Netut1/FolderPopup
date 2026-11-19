using System.IO;
using System.Windows;
namespace FolderPopup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 1 && e.Args[0] == "--cleanup")
        {
            foreach (var f in Config.Load()) ShortcutHelper.Delete(f);
            ShortcutHelper.Notify();
            try { Directory.Delete(Config.Dir, true); } catch { }
            Shutdown();
            return;
        }

        if (e.Args.Length >= 2 && e.Args[0] == "--open")
        {
            var folder = e.Args[1];
            var dropped = e.Args.Skip(2).ToList();   // файлы, брошенные на ярлык
            if (dropped.Count > 0) { AddToFolder(folder, dropped); Shutdown(); return; }
            new PopupWindow(folder).Show();
        }
        else new MainWindow().Show();
    }

    static void AddToFolder(string folder, List<string> paths)
    {
        foreach (var p in paths)
        {
            var name = Path.GetFileName(p);
            if (string.Equals(name, Path.GetFileName(folder) + ".lnk", StringComparison.OrdinalIgnoreCase))
                continue;   // ярлык самой папки не двигаем

            var dest = Path.Combine(folder, name);
            int i = 2;
            while (File.Exists(dest) || Directory.Exists(dest))
                dest = Path.Combine(folder,
                    Path.GetFileNameWithoutExtension(name) + $" ({i++})" + Path.GetExtension(name));

            try
            {
                if (Directory.Exists(p)) Directory.Move(p, dest);
                else File.Move(p, dest);
            }
            catch (UnauthorizedAccessException) when (File.Exists(p))
            {
                File.Copy(p, dest);   // «общий» рабочий стол: переносить нельзя, копируем
            }
            catch { }
        }

        ShortcutHelper.Create(folder, IconBuilder.Build(folder));
        ShortcutHelper.Notify();
    }
}