using System.IO;
using System.Text.Json;
namespace FolderPopup;

public static class Config
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderPopup");
    static string ConfigFile => Path.Combine(Dir, "config.json");

    public static List<string> Load()
    {
        try { return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ConfigFile)) ?? new(); }
        catch { return new(); }
    }

    public static void Save(List<string> list)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(list));
    }
}
