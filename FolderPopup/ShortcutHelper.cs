using System.IO;
using System.Runtime.InteropServices;
namespace FolderPopup;

public static class ShortcutHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern void SHChangeNotify(int eventId, uint flags, string path, IntPtr item2);

    // Сообщаем Проводнику об изменении только этого ярлыка (SHCNE_UPDATEITEM, SHCNF_PATHW)
    static void UpdateItem(string path) => SHChangeNotify(0x00002000, 0x0005, path, IntPtr.Zero);

    static string LnkPath(string folder) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Path.GetFileName(folder) + ".lnk");

    static dynamic Shell() => Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;

    public static void Create(string folder, string icoPath)
    {
        var lnk = LnkPath(folder);
        var s = Shell().CreateShortcut(lnk);
        s.TargetPath = Environment.ProcessPath;
        s.Arguments = $"--open \"{folder}\"";
        s.IconLocation = icoPath + ",0";
        s.Save();
        UpdateItem(lnk);
        IconBuilder.Cleanup(icoPath);   // старые иконки этой папки больше не нужны
    }

    // Обновляет иконку существующего ярлыка, если она не совпадает с актуальной
    public static void Refresh(string folder)
    {
        var lnk = LnkPath(folder);
        if (!File.Exists(lnk)) return;
        var ico = IconBuilder.Build(folder);
        string cur = Shell().CreateShortcut(lnk).IconLocation;
        if (cur.StartsWith(ico, StringComparison.OrdinalIgnoreCase)) return;
        Create(folder, ico);
    }

    // Удаляет ярлык и иконки этой папки; сами файлы в папке не трогает
    public static void Delete(string folder)
    {
        try
        {
            var lnk = LnkPath(folder);
            if (File.Exists(lnk))
            {
                string args = Shell().CreateShortcut(lnk).Arguments;
                if (args.StartsWith("--open"))   // удаляем только наш ярлык
                    File.Delete(lnk);
            }
            var icons = IconBuilder.IconDir(folder);
            if (Directory.Exists(icons)) Directory.Delete(icons, true);
        }
        catch { }
    }

    // Оставлено для совместимости: глобальное обновление всех иконок больше не вызываем
    public static void Notify() { }
}