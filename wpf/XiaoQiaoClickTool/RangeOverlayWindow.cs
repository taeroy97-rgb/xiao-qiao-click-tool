using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XiaoQiaoClickTool;

public sealed class RangeOverlayWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Shape _circle;
    private readonly Shape _rectangle;
    private readonly Ellipse _centerDot;

    public RangeOverlayWindow()
    {
        Width = 220;
        Height = 220;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;
        Content = _canvas;

        var stroke = new LinearGradientBrush(Color.FromRgb(55, 190, 255), Color.FromRgb(111, 54, 236), 30);
        _circle = new Ellipse { Stroke = stroke, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromArgb(28, 54, 126, 255)) };
        _rectangle = new Rectangle { Stroke = stroke, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromArgb(28, 54, 126, 255)), RadiusX = 12, RadiusY = 12 };
        _centerDot = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(38, 123, 255)) };
        _canvas.Children.Add(_circle);
        _canvas.Children.Add(_rectangle);
        _canvas.Children.Add(_centerDot);
        SourceInitialized += (_, _) => MakeClickThrough();
    }

    public void UpdatePreview(Point cursor, RangeMode mode, int size, bool enabled)
    {
        var dpiScale = GetDpiScaleForPoint((int)cursor.X, (int)cursor.Y);
        var previewSizePx = Math.Max(24, size * 2);
        var windowSizePx = previewSizePx + 70;
        var previewSize = previewSizePx / dpiScale;
        var windowSize = windowSizePx / dpiScale;

        if (!enabled)
        {
            _circle.Visibility = Visibility.Collapsed;
            _rectangle.Visibility = Visibility.Collapsed;
        }
        else
        {
            _circle.Visibility = mode == RangeMode.Circle ? Visibility.Visible : Visibility.Collapsed;
            _rectangle.Visibility = mode == RangeMode.Rectangle ? Visibility.Visible : Visibility.Collapsed;
        }

        Width = windowSize;
        Height = windowSize;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, (int)Math.Round(cursor.X - windowSizePx / 2.0), (int)Math.Round(cursor.Y - windowSizePx / 2.0), windowSizePx, windowSizePx, SWP_NOACTIVATE);
        }
        else
        {
            Left = cursor.X / dpiScale - windowSize / 2;
            Top = cursor.Y / dpiScale - windowSize / 2;
        }

        _circle.Width = previewSize;
        _circle.Height = previewSize;
        Canvas.SetLeft(_circle, (windowSize - previewSize) / 2);
        Canvas.SetTop(_circle, (windowSize - previewSize) / 2);

        _rectangle.Width = previewSize;
        _rectangle.Height = previewSize;
        if (_rectangle is Rectangle rectangle)
        {
            rectangle.RadiusX = 12 / dpiScale;
            rectangle.RadiusY = 12 / dpiScale;
        }
        Canvas.SetLeft(_rectangle, (windowSize - previewSize) / 2);
        Canvas.SetTop(_rectangle, (windowSize - previewSize) / 2);

        _centerDot.Width = 9 / dpiScale;
        _centerDot.Height = 9 / dpiScale;
        Canvas.SetLeft(_centerDot, windowSize / 2 - _centerDot.Width / 2);
        Canvas.SetTop(_centerDot, windowSize / 2 - _centerDot.Height / 2);
    }

    private static double GetDpiScaleForPoint(int x, int y)
    {
        var monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
        {
            return dpiX / 96.0;
        }

        return 1.0;
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int SWP_NOACTIVATE = 0x0010;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int MDT_EFFECTIVE_DPI = 0;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
