using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Sniffer
// Usage:
//   Sniffer.exe <pid> [outPath]
//
// Notes:
// - PID must be a console-attached client process (e.g. cmd.exe running the app), not conhost.exe.
// - Captures the current console screen buffer contents (text).

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ReadConsoleOutputCharacterW(
        IntPtr hConsoleOutput,
        [Out] char[] lpCharacter,
        uint nLength,
        COORD dwReadCoord,
        out uint lpNumberOfCharsRead);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
        public COORD(short x, short y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    public static int Main(string[] args)
    {
        if (args.Length < 1 || !uint.TryParse(args[0], out uint pid))
        {
            Console.Error.WriteLine("Usage: Sniffer.exe <pid> [outPath]");
            return 2;
        }

        string outPath = args.Length >= 2
            ? args[1]
            : Path.Combine(AppContext.BaseDirectory, "capture.txt");

        try
        {
            FreeConsole();

            if (!AttachConsole(pid))
            {
                int err = Marshal.GetLastWin32Error();
                WriteFailure(outPath, pid, $"AttachConsole({pid}) failed. Win32Error={err}");
                return 3;
            }

            IntPtr hConOut = CreateFileW(
                "CONOUT$",
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hConOut == new IntPtr(-1))
            {
                int err = Marshal.GetLastWin32Error();
                FreeConsole();
                WriteFailure(outPath, pid, $"CreateFileW(CONOUT$) failed. Win32Error={err}");
                return 4;
            }

            if (!GetConsoleScreenBufferInfo(hConOut, out var info))
            {
                int err = Marshal.GetLastWin32Error();
                CloseHandle(hConOut);
                FreeConsole();
                WriteFailure(outPath, pid, $"GetConsoleScreenBufferInfo failed. Win32Error={err}");
                return 5;
            }

            int width = info.dwSize.X;
            int height = info.dwSize.Y;

            var sb = new StringBuilder(width * height + height);
            sb.AppendLine($"[PID={pid}] BufferSize={width}x{height} Cursor=({info.dwCursorPosition.X},{info.dwCursorPosition.Y})");
            sb.AppendLine(new string('-', Math.Min(width, 120)));

            var row = new char[width];
            for (short y = 0; y < height; y++)
            {
                if (!ReadConsoleOutputCharacterW(hConOut, row, (uint)width, new COORD(0, y), out uint read))
                {
                    int err = Marshal.GetLastWin32Error();
                    sb.AppendLine($"[Read error at row {y}] Win32Error={err}");
                    break;
                }
                sb.Append(row, 0, (int)read);
                sb.AppendLine();
            }

            CloseHandle(hConOut);
            FreeConsole();

            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine(outPath);
            return 0;
        }
        catch (Exception ex)
        {
            try { WriteFailure(outPath, pid, "Unhandled exception", ex); } catch { }
            return 1;
        }
    }

    private static void WriteFailure(string outPath, uint pid, string message, Exception? ex = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[PID={pid}] Sniffer FAILED");
        sb.AppendLine(message);
        if (ex != null)
        {
            sb.AppendLine();
            sb.AppendLine(ex.ToString());
        }

        try { File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false)); } catch { }

        Console.WriteLine(outPath);
        Console.Error.WriteLine(message);
        if (ex != null) Console.Error.WriteLine(ex);
    }
}
