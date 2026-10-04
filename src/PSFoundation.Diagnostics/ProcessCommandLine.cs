using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace PSFoundation.Diagnostics;

/// <summary>Parses Windows executable command lines. Does not expand environment variables or evaluate shell syntax.</summary>
public sealed class ProcessCommandLine
{
    public string FileName { get; }
    public IReadOnlyList<string> Arguments { get; }
    private ProcessCommandLine(string[] values) { FileName = values[0]; Arguments = Array.AsReadOnly(values.Skip(1).ToArray()); }
    public static ProcessCommandLine Parse(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || commandLine.IndexOf('\0') >= 0 || commandLine.Length > 32767)
            throw new ArgumentException("A nonempty Windows command line of at most 32767 characters is required.", nameof(commandLine));
        var memory = CommandLineToArgvW(commandLine.Trim(), out var count);
        if (memory == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var values = new string[count];
            for (var i = 0; i < count; i++)
                values[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory, i * IntPtr.Size))!;
            if (count == 0 || string.IsNullOrWhiteSpace(values[0]))
                throw new ArgumentException("An executable is required.", nameof(commandLine));
            return new ProcessCommandLine(values);
        }
        finally { LocalFree(memory); }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
