using Microsoft.Win32;
namespace FolderPopup;

public static class Theme
{
    // true, если у Windows включена светлая тема приложений
    public static bool IsLight
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var v = k?.GetValue("AppsUseLightTheme");
                return v is int i ? i == 1 : true;
            }
            catch { return true; }
        }
    }
}
