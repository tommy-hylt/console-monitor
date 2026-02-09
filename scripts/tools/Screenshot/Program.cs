using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            // Usage:
            //   Screenshot.exe --pid <pid> --png <out.png> [--json <out.json>] [--title "Title hint"]
            //   Screenshot.exe <pid> <out.png>
            // Output:
            //   Writes JSON to the json file (NOT stdout, because AttachConsole can steal stdout).

            int? pid = null;
            string? pngPath = null;
            string? jsonPath = null;
            string? titleHint = null;

            var positional = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a == "--pid" && i + 1 < args.Length)
                {
                    pid = int.Parse(args[++i]);
                }
                else if ((a == "--png" || a == "--out") && i + 1 < args.Length)
                {
                    pngPath = args[++i];
                }
                else if (a == "--json" && i + 1 < args.Length)
                {
                    jsonPath = args[++i];
                }
                else if (a == "--title" && i + 1 < args.Length)
                {
                    titleHint = args[++i];
                }
                else
                {
                    positional.Add(a);
                }
            }

            if (pid is null && positional.Count >= 1)
            {
                pid = int.Parse(positional[0]);
            }
            if (pngPath is null && positional.Count >= 2)
            {
                pngPath = positional[1];
            }

            if (pid is null || pid <= 0 || string.IsNullOrWhiteSpace(pngPath))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Usage: Screenshot.exe --pid <pid> --png <out.png> [--json out.json] [--title titleHint]" }));
                return 2;
            }

            pngPath = Path.GetFullPath(pngPath!);
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                jsonPath = pngPath + ".json";
            }
            jsonPath = Path.GetFullPath(jsonPath!);

            Directory.CreateDirectory(Path.GetDirectoryName(pngPath) ?? AppContext.BaseDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? AppContext.BaseDirectory);

            // Resolve the actual console window handle via AttachConsole(pid) + GetConsoleWindow().
            // (Console window is usually owned by conhost.exe, not the client process.)
            IntPtr hwnd = IntPtr.Zero;
            string title = string.Empty;
            string className = string.Empty;

            // Detach from our own console first (best-effort), then attach to target.
            FreeConsole();

            if (AttachConsole((uint)pid.Value))
            {
                try
                {
                    hwnd = GetConsoleWindow();
                    title = GetWindowText(hwnd);
                    className = GetClassName(hwnd);
                }
                finally
                {
                    FreeConsole();
                }
            }

            // Fallback: ConPTY / Windows Terminal etc.
            if ((hwnd == IntPtr.Zero || !IsWindow(hwnd)) && !string.IsNullOrWhiteSpace(titleHint))
            {
                var found = FindBestWindowByTitle(titleHint!);
                hwnd = found.Hwnd;
                title = found.Title;
                className = found.ClassName;
            }

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(new { ok = false, pid, titleHint, error = "NO_CONSOLE_WINDOW" }));
                return 3;
            }

            var win = new Win(hwnd, title, className, IsWindowVisible(hwnd));

            var bounds = GetExtendedFrameBounds(win.Hwnd);
            if (bounds.Width <= 0 || bounds.Height <= 0) bounds = GetWindowRect(win.Hwnd);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(new { ok = false, pid, hwnd = ToHex(win.Hwnd), title = win.Title, className = win.ClassName, error = "NO_BOUNDS" }));
                return 4;
            }

            using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Transparent);

            string method = "PrintWindow";
            bool captured;
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try
                {
                    captured = PrintWindow(win.Hwnd, hdc, PW_RENDERFULLCONTENT);
                    if (!captured)
                    {
                        method = "WM_PRINT";
                        captured = SendPrintMessage(win.Hwnd, hdc);
                    }
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }

            if (!captured)
            {
                File.WriteAllText(jsonPath, JsonSerializer.Serialize(new { ok = false, pid, hwnd = ToHex(win.Hwnd), title = win.Title, className = win.ClassName, error = $"CAPTURE_FAILED:{Marshal.GetLastWin32Error()}" }));
                return 5;
            }

            bmp.Save(pngPath!, ImageFormat.Png);

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
            {
                ok = true,
                pid,
                hwnd = ToHex(win.Hwnd),
                title = win.Title,
                className = win.ClassName,
                png = pngPath,
                method,
            }));

            Console.WriteLine("OK");
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                // Best-effort stderr/console.
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
            }
            catch { }
            return 1;
        }
    }

    private static Win FindBestWindowByTitle(string match)
    {
        match = match.Trim();
        var wins = EnumerateTopLevelWindows();

        // Exact (visible, non-empty)
        foreach (var w in wins)
        {
            if (!w.IsVisible) continue;
            if (string.IsNullOrWhiteSpace(w.Title)) continue;
            if (w.Title.Equals(match, StringComparison.Ordinal)) return w;
        }
        // Substring (visible)
        foreach (var w in wins)
        {
            if (!w.IsVisible) continue;
            if (string.IsNullOrWhiteSpace(w.Title)) continue;
            if (w.Title.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0) return w;
        }
        return default;
    }

    private static List<Win> EnumerateTopLevelWindows()
    {
        var result = new List<Win>();
        EnumWindows((hwnd, lParam) =>
        {
            var title = GetWindowText(hwnd);
            var cls = GetClassName(hwnd);
            bool vis = IsWindowVisible(hwnd);
            result.Add(new Win(hwnd, title, cls, vis));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool SendPrintMessage(IntPtr hwnd, IntPtr hdc)
    {
        const int WM_PRINT = 0x0317;
        const int PRF_CHECKVISIBLE = 0x00000001;
        const int PRF_NONCLIENT = 0x00000002;
        const int PRF_CLIENT = 0x00000004;
        const int PRF_ERASEBKGND = 0x00000008;
        const int PRF_CHILDREN = 0x00000010;
        const int PRF_OWNED = 0x00000020;

        IntPtr res = SendMessage(hwnd, WM_PRINT, hdc, (IntPtr)(PRF_CLIENT | PRF_NONCLIENT | PRF_ERASEBKGND | PRF_CHILDREN | PRF_OWNED | PRF_CHECKVISIBLE));
        return IsWindow(hwnd) && res != IntPtr.Zero || IsWindow(hwnd);
    }

    private static string ToHex(IntPtr hwnd) => $"0x{hwnd.ToInt64():X}";

    private static Rectangle GetExtendedFrameBounds(IntPtr hwnd)
    {
        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>()) == 0)
        {
            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
        return Rectangle.Empty;
    }

    private static Rectangle GetWindowRect(IntPtr hwnd)
    {
        if (GetWindowRect(hwnd, out RECT r))
        {
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
        return Rectangle.Empty;
    }

    private readonly record struct Win(IntPtr Hwnd, string Title, string ClassName, bool IsVisible);

    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static string GetWindowText(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;
        int len = GetWindowTextLengthW(hwnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowTextW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;
        var sb = new StringBuilder(256);
        GetClassNameW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
