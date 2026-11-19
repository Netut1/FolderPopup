using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
namespace FolderPopup;

public partial class PopupWindow : Window
{
    const int Cols = 4, Rows = 4, PageSize = Cols * Rows;

    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int L, R, T, B; }

    readonly string _root;                  // папка, на которую ведёт ярлык
    readonly Stack<string> _back = new();   // история для кнопки «Назад»
    string _current;
    List<string> _items = new();
    int _page;
    volatile int _loadId;                   // отменяет устаревшую загрузку иконок

    readonly bool _light = Theme.IsLight;
    readonly Color _fgColor;
    readonly Brush _fg;

    public PopupWindow(string folder)
    {
        InitializeComponent();
        Left = Top = -10000;                // пока не посчитали позицию, окно за экраном
        _root = _current = folder;
        Cells.Columns = Cols; Cells.Rows = Rows;

        _fgColor = _light ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Colors.White;
        _fg = new SolidColorBrush(_fgColor);
        FolderTitle.Foreground = BackBtn.Foreground = OpenBtn.Foreground = _fg;

        Deactivated += (_, _) => Close();
        SourceInitialized += OnSourceInit;
        Loaded += OnLoaded;

        Navigate(folder);
    }

    void OnSourceInit(object? s, EventArgs e)
    {
        var h = new WindowInteropHelper(this).Handle;
        if (Environment.OSVersion.Version.Build >= 22621)   // Windows 11 22H2+: акрил
        {
            HwndSource.FromHwnd(h).CompositionTarget.BackgroundColor = Colors.Transparent;
            var m = new MARGINS { L = -1, R = -1, T = -1, B = -1 };
            DwmExtendFrameIntoClientArea(h, ref m);
            int v = _light ? 0 : 1; DwmSetWindowAttribute(h, 20, ref v, 4);   // тема окна
            v = 3; DwmSetWindowAttribute(h, 38, ref v, 4);   // акрил
            v = 2; DwmSetWindowAttribute(h, 33, ref v, 4);   // скруглённые углы
        }
        else   // запасной вариант для Windows 10: полупрозрачный фон
        {
            Background = new SolidColorBrush(_light ? Color.FromArgb(245, 245, 245, 250)
                                                    : Color.FromArgb(245, 32, 32, 38));
        }
    }

    void OnLoaded(object? s, RoutedEventArgs e)
    {
        GetCursorPos(out var p);
        var dpi = VisualTreeHelper.GetDpi(this);
        double x = p.X / dpi.DpiScaleX, y = p.Y / dpi.DpiScaleY;
        var wa = SystemParameters.WorkArea;
        Left = Math.Clamp(x - ActualWidth / 2, wa.Left, wa.Right - ActualWidth);
        Top = Math.Clamp(y - ActualHeight / 2, wa.Top, wa.Bottom - ActualHeight);
        Activate();
        RefreshShortcutIcon();
    }

    void Navigate(string folder)
    {
        _current = folder;
        _items = IconBuilder.GetItems(folder);
        FolderTitle.Text = Path.GetFileName(folder.TrimEnd('\\'));
        BackBtn.Visibility = _back.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowPage(0, force: true);
    }

    void GoBack() { if (_back.Count > 0) Navigate(_back.Pop()); }

    void ShowPage(int page, bool force = false)
    {
        int pages = Math.Max(1, (_items.Count + PageSize - 1) / PageSize);
        int p = Math.Clamp(page, 0, pages - 1);
        if (p == _page && !force) return;
        _page = p;
        Cells.Children.Clear();
        var jobs = new List<(string path, Image img)>();
        foreach (var path in _items.Skip(_page * PageSize).Take(PageSize))
        {
            var img = new Image { Width = 48, Height = 48, Margin = new Thickness(0, 0, 0, 6) };
            Cells.Children.Add(MakeCell(path, img));
            jobs.Add((path, img));
        }

        Dots.Children.Clear();
        if (pages > 1)
            for (int i = 0; i < pages; i++)
                Dots.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Margin = new Thickness(3, 0, 3, 0),
                    Fill = new SolidColorBrush(Color.FromArgb(i == _page ? (byte)255 : (byte)90,
                                                              _fgColor.R, _fgColor.G, _fgColor.B))
                });

        LoadIcons(jobs);
    }

    // Иконки грузятся в фоне, чтобы медленный ярлык не тормозил окно
    void LoadIcons(List<(string path, Image img)> jobs)
    {
        int id = ++_loadId;
        var t = new Thread(() =>
        {
            foreach (var (path, img) in jobs)
            {
                if (id != _loadId) return;
                var bmp = IconBuilder.GetIcon(path, 64);
                Dispatcher.InvokeAsync(() => { if (id == _loadId) img.Source = bmp; });
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    UIElement MakeCell(string path, Image img)
    {
        var name = Directory.Exists(path) ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(img);
        panel.Children.Add(new TextBlock
        {
            Text = name,
            Foreground = _fg,
            FontSize = 12,
            MaxHeight = 32,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 0, 4, 0)
        });
        var hover = new SolidColorBrush(_light ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(40, 255, 255, 255));
        var cell = new Border
        {
            Width = 92,
            Height = 100,
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            Child = panel,
            Cursor = Cursors.Hand
        };
        cell.MouseEnter += (_, _) => cell.Background = hover;
        cell.MouseLeave += (_, _) => cell.Background = Brushes.Transparent;
        cell.MouseLeftButtonUp += (_, _) => OpenItem(path);
        // ПКМ: подпапка открывается в Проводнике; для файла или ярлыка открывается показанная сейчас папка
        cell.MouseRightButtonUp += (_, _) => OpenInExplorer(Directory.Exists(path) ? path : _current);
        return cell;
    }

    void OpenItem(string path)
    {
        if (Directory.Exists(path)) { _back.Push(_current); Navigate(path); return; }   // попап в попапе
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        Close();
    }

    void OpenInExplorer(string folder)
    {
        try { Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); } catch { }
        Close();
    }

    void Back_Click(object sender, RoutedEventArgs e) => GoBack();
    void Open_Click(object sender, RoutedEventArgs e) => OpenInExplorer(_current);

    // Иконка ярлыка на рабочем столе пересобирается при открытии, если состав папки изменился
    void RefreshShortcutIcon()
    {
        var root = _root;
        var t = new Thread(() => { try { ShortcutHelper.Refresh(root); } catch { } });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (_back.Count > 0) GoBack(); else Close(); }
        else if (e.Key == Key.Right) ShowPage(_page + 1);
        else if (e.Key == Key.Left) ShowPage(_page - 1);
    }

    void Window_MouseWheel(object sender, MouseWheelEventArgs e) =>
        ShowPage(_page + (e.Delta < 0 ? 1 : -1));
}