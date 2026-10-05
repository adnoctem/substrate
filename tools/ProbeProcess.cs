using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace AdNoctem.Substrate.DevTools
{
    // C# 5 syntax is intentional: Windows PowerShell 5.1 compiles this test-only helper with Add-Type.
    public static class ProbeProcess
    {
        public sealed class Result
        {
            public int ExitCode { get; private set; }
            public string Output { get; private set; }
            public Result(int exitCode, string output) { ExitCode = exitCode; Output = output; }
        }

        // The job owns the probe and its children. Closing the runner or the job kills them, including after an interrupted test.
        // Fail closed if Windows cannot establish the limits; never retry an unbounded probe.
        public static Result Run(string executable, string[] arguments, int timeoutMilliseconds, int memoryLimitMiB)
        {
            if (timeoutMilliseconds < 1 || memoryLimitMiB < 32)
                throw new ArgumentOutOfRangeException();
            using (var job = CreateJobObject(IntPtr.Zero, IntPtr.Zero))
            using (var process = new Process())
            {
                if (job.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var limits = new ExtendedLimit();
                limits.Basic.Flags = 0x2000 | 0x200; // KILL_ON_JOB_CLOSE | JOB_MEMORY
                limits.JobMemory = new UIntPtr(checked((ulong)memoryLimitMiB * 1024 * 1024));
                if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimit))))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var commandLine = new StringBuilder();
                foreach (var argument in arguments)
                {
                    if (commandLine.Length != 0)
                        commandLine.Append(' ');
                    commandLine.Append(Quote(argument));
                }
                process.StartInfo = new ProcessStartInfo(executable, commandLine.ToString())
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                // A raw Process launch does not apply PowerShell's cross-edition PSModulePath cleanup.
                // Let the child engine construct its own standard module search path.
                process.StartInfo.EnvironmentVariables.Remove("PSModulePath");
                process.Start();
                try
                {
                    if (!AssignProcessToJobObject(job, process.Handle))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    var output = Capture(process.StandardOutput);
                    var error = Capture(process.StandardError);
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        job.Dispose();
                        if (!process.WaitForExit(5000))
                            throw new IOException("The timed-out probe did not terminate.");
                        throw new TimeoutException("PowerShell probe exceeded its time limit.");
                    }
                    job.Dispose();
                    if (!Task.WaitAll(new Task[] { output, error }, 5000))
                        throw new IOException("Probe output did not close.");
                    return new Result(process.ExitCode, output.Result + error.Result);
                }
                finally
                {
                    job.Dispose();
                    if (!process.HasExited)
                    { process.Kill(); process.WaitForExit(5000); }
                }
            }
        }

        private static async Task<string> Capture(StreamReader reader)
        {
            var text = new StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                text.Append(buffer, 0, Math.Min(count, 131072 - text.Length));
            return text.ToString();
        }
        private static string Quote(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0)
                throw new ArgumentException("Invalid process argument.");
            var text = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in value)
            {
                if (character == '\\')
                { slashes++; continue; }
                text.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character);
                slashes = 0;
            }
            return text.Append('\\', slashes * 2).Append('"').ToString();
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimit
        {
            public long ProcessTime, JobTime;
            public uint Flags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcesses;
            public UIntPtr Affinity;
            public uint Priority, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimit
        {
            public BasicLimit Basic;
            public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
            public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }
        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr attributes, IntPtr name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimit limits, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
