using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

// Foreground
// Usage:
//   Foreground.exe <pid>
//
// Best-effort:
// - If PID is a classic console client: attach and bring its console window.
// - If PID is a Windows Terminal/ConPTY client (no console window): find ancestor WindowsTerminal.exe and bring that window.

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public static int Main(string[] args)
    {
        if (args.Length < 1 || !uint.TryParse(args[0], out uint pid))
        {
            Console.Error.WriteLine("Usage: Foreground.exe <pid>");
            return 2;
        }

        // Try classic console attach first.
        try
        {
            FreeConsole();
            if (AttachConsole(pid))
            {
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);

                IntPtr hwnd = GetConsoleWindow();
                if (hwnd != IntPtr.Zero)
                {
                    BringToFront(hwnd);
                    return 0;
                }
            }
        }
        catch { }
        finally
        {
            try { FreeConsole(); } catch { }
        }

        // Fallback: bring ancestor WindowsTerminal window.
        try
        {
            int? wtPid = FindAncestorWindowsTerminalPid((int)pid);
            if (wtPid.HasValue)
            {
                using var wt = Process.GetProcessById(wtPid.Value);
                if (wt.MainWindowHandle != IntPtr.Zero)
                {
                    BringToFront(wt.MainWindowHandle);
                    return 0;
                }
            }
        }
        catch { }

        return 4;
    }

    private static int? FindAncestorWindowsTerminalPid(int pid)
    {
        // Walk parent chain via WMI.
        int cur = pid;
        for (int i = 0; i < 8; i++)
        {
            var (ppid, name) = GetParentInfo(cur);
            if (ppid <= 0) return null;
            if (!string.IsNullOrEmpty(name) && name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase))
                return ppid; // parent is WindowsTerminal.exe
            cur = ppid;
        }
        return null;
    }

    private static (int parentPid, string? parentName) GetParentInfo(int pid)
    {
        // Query this process to get its parent PID.
        using var searcher = new ManagementObjectSearcher($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId={pid}");
        using var results = searcher.Get();
        foreach (ManagementObject mo in results)
        {
            int ppid = Convert.ToInt32(mo["ParentProcessId"]);
            string? pname = null;
            try
            {
                using var searcher2 = new ManagementObjectSearcher($"SELECT Name FROM Win32_Process WHERE ProcessId={ppid}");
                using var res2 = searcher2.Get();
                foreach (ManagementObject mo2 in res2)
                {
                    pname = mo2["Name"] as string;
                    break;
                }
            }
            catch { }
            return (ppid, pname);
        }
        return (0, null);
    }

    private static void BringToFront(IntPtr hwnd)
    {
        IntPtr fg = GetForegroundWindow();
        uint fgThread = fg != IntPtr.Zero ? GetWindowThreadProcessId(fg, out _) : 0;
        uint targetThread = GetWindowThreadProcessId(hwnd, out _);
        uint curThread = GetCurrentThreadId();

        if (fgThread != 0 && fgThread != curThread)
            AttachThreadInput(curThread, fgThread, true);
        if (targetThread != 0 && targetThread != curThread)
            AttachThreadInput(curThread, targetThread, true);

        try
        {
            ShowWindow(hwnd, SW_RESTORE);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
            Thread.Sleep(100);
        }
        finally
        {
            if (targetThread != 0 && targetThread != curThread)
                AttachThreadInput(curThread, targetThread, false);
            if (fgThread != 0 && fgThread != curThread)
                AttachThreadInput(curThread, fgThread, false);
        }
    }
}
