using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FolderPopup;

public static class IconBuilder
{
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr hbitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);

    public static List<string> GetItems(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Where(i => !i.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(i => i.FullName).ToList();
        }
        catch { return new(); }
    }

    // Чёткая иконка через Shell (до 256 px); если не вышло, старый способ
    public static BitmapSource? GetIcon(string path, int size = 64)
    {
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var f) == 0 &&
                f.GetImage(new SIZE { cx = size, cy = size }, 0x4 | 0x1, out var hbm) == 0)
            {
                try
                {
                    var bs = Imaging.CreateBitmapSourceFromHBitmap(
                        hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bs.Freeze();
                    return bs;
                }
                finally { DeleteObject(hbm); }
            }
        }
        catch { }
        return GetIconOld(path);
    }

    static BitmapSource? GetIconOld(string path)
    {
        try
        {
            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (ico == null) return null;
            var bs = Imaging.CreateBitmapSourceFromHIcon(
                ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        catch { return null; }
    }

    static string Hash(string s) => Convert.ToHexString(
        System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(s))).Substring(0, 8);

    public static string IconDir(string folder) => Path.Combine(
        Config.Dir, "icons", Hash(folder.ToLowerInvariant()));

    // Рисует иконку 3x3 и возвращает путь к .ico (имя зависит от состава папки и темы)
    public static string Build(string folder)
    {
        var items = GetItems(folder).Take(9).ToList();
        bool light = Theme.IsLight;

        var dir = IconDir(folder);
        Directory.CreateDirectory(dir);
        var key = light + "|" + string.Join("|", items);
        var icoPath = Path.Combine(dir, Hash(key) + ".ico");
        if (File.Exists(icoPath)) return icoPath;

        const int S = 256;
        var dv = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
        using (var dc = dv.RenderOpen())
        {
            var tile = light ? Color.FromArgb(225, 246, 246, 250) : Color.FromArgb(210, 40, 40, 48);
            dc.DrawRoundedRectangle(new SolidColorBrush(tile), null, new Rect(8, 8, S - 16, S - 16), 48, 48);

            double pad = 30, cell = (S - 2 * pad) / 3;
            for (int i = 0; i < items.Count; i++)
            {
                var bmp = GetIcon(items[i], 96);
                if (bmp == null) continue;dc.DrawImage(bmp, new Rect(pad + (i % 3) * cell + 6, pad + (i / 3) * cell + 6, cell - 12, cell - 12));
            }
        }
        var rtb = new RenderTargetBitmap(S, S, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        SaveIco(rtb, icoPath);
        return icoPath;
    }

    // Удаляет старые иконки этой папки, оставляя только актуальную
    public static void Cleanup(string keep)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Path.GetDirectoryName(keep)!, "*.ico"))
                if (!string.Equals(f, keep, StringComparison.OrdinalIgnoreCase))
                    try { File.Delete(f); } catch { }
        }
        catch { }
    }

    static void SaveIco(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        var png = ms.ToArray();

        var tmp = path + ".tmp";   // сначала временный файл, чтобы не остался битый .ico
        using (var w = new BinaryWriter(File.Create(tmp)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)1);
            w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(png.Length); w.Write(22);
            w.Write(png);
        }
        File.Move(tmp, path, true);
    }
}