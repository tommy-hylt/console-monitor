using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

// List
// Writes JSON array to a file: [{ pid, title, path }]
//
// Re-added AttachConsole per Tommy.
// Purpose: get a meaningful console title (GetConsoleTitleW) and confirm the PID is console-attachable.
//
// IMPORTANT:
// While attached to another process's console, stdout/stderr may effectively go to that console.
// To avoid writing results into random consoles (or appearing empty), we write to a JSON file.
//
// Usage:
//   List.exe [outPath]
// If outPath is omitted, writes "list.json" next to the executable.

internal sealed record Item(int pid, string title, string? path);

internal static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetConsoleTitleW([Out] StringBuilder lpConsoleTitle, uint nSize);

    public static int Main(string[] args)
    {
        // Decide output file first; do NOT rely on Console once we start attaching.
        string outPath = args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : System.IO.Path.Combine(AppContext.BaseDirectory, "list.json");

        try
        {
            var items = new List<Item>();

            // Ensure we start detached.
            try { FreeConsole(); } catch { }

            foreach (var p in Process.GetProcesses())
            {
                int pid;
                try { pid = p.Id; } catch { continue; }
                if (pid <= 0) continue;
                if (pid == Environment.ProcessId) continue;

                string? exePath = null;
                try { exePath = p.MainModule?.FileName; } catch { }

                string title = string.Empty;

                // Prefer real console title if attach works.
                bool attached = false;
                try
                {
                    FreeConsole();
                    attached = AttachConsole((uint)pid);
                    if (attached)
                    {
                        var sb = new StringBuilder(1024);
                        var n = GetConsoleTitleW(sb, (uint)sb.Capacity);
                        if (n > 0)
                            title = sb.ToString().Trim();
                    }
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    try { FreeConsole(); } catch { }
                }

                if (string.IsNullOrEmpty(title))
                {
                    // Fallback to MainWindowTitle, then ProcessName
                    try { title = (p.MainWindowTitle ?? string.Empty).Trim(); } catch { }
                    if (string.IsNullOrEmpty(title))
                    {
                        try
                        {
                            var name = (p.ProcessName ?? string.Empty).Trim();
                            title = name.Length > 0 ? name : "(no-title)";
                        }
                        catch
                        {
                            title = "(no-title)";
                        }
                    }
                }

                // Only include console-attachable processes (these are the ones we can sniff).
                if (!attached) continue;

                items.Add(new Item(pid, title, exePath));
            }

            var outList = items
                .GroupBy(i => i.pid)
                .Select(g => g.First())
                .OrderBy(i => i.pid)
                .ToList();

            var json = JsonSerializer.Serialize(outList);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath) ?? AppContext.BaseDirectory);
            System.IO.File.WriteAllText(outPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Print outPath for callers (machine-friendly)
            try { Console.WriteLine(outPath); } catch { }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
