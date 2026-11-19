using System.IO;
using System.Runtime.InteropServices;
namespace FolderPopup;

public static class ShortcutHelper
{
    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, int flags, IntPtr a, IntPtr b);

    static string LnkPath(string folder) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Path.GetFileName(folder) + ".lnk");

    static dynamic Shell() => Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;

    public static void Create(string folder, string icoPath)
    {
        var s = Shell().CreateShortcut(LnkPath(folder));
        s.TargetPath = Environment.ProcessPath;
        s.Arguments = $"--open \"{folder}\"";
        s.IconLocation = icoPath + ",0";
        s.Save();
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
        Notify();
    }

    public static void Delete(string folder)
    {
        try
        {
            var lnk = LnkPath(folder);
            if (File.Exists(lnk))
            {
                string args = Shell().CreateShortcut(lnk).Arguments;
                if (args.StartsWith("--open")) File.Delete(lnk);
            }
            var icons = IconBuilder.IconDir(folder);
            if (Directory.Exists(icons)) Directory.Delete(icons, true);
        }
        catch { }
    }

    public static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
}