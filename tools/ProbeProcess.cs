using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

            public Result(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }
        }

        // The job owns the probe and its children. Closing the runner or the job kills them, including after an interrupted test.
        // Fail closed if Windows cannot establish the limits; never retry an unbounded probe.
        public static Result Run(
            string executable,
            string[] arguments,
            int timeoutMilliseconds,
            int memoryLimitMiB
        )
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

                if (
                    !SetInformationJobObject(
                        job,
                        9,
                        ref limits,
                        (uint)Marshal.SizeOf(typeof(ExtendedLimit))
                    )
                )
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var commandLine = new StringBuilder();

                foreach (var argument in arguments)
                {
                    if (commandLine.Length != 0)
                        commandLine.Append(' ');

                    commandLine.Append(Quote(argument));
                }

                process.StartInfo = new ProcessStartInfo(executable, commandLine.ToString())
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                // A raw Process launch does not apply PowerShell's cross-edition PSModulePath cleanup.
                // Let the child engine construct its own standard module search path.
                process.StartInfo.EnvironmentVariables.Remove("PSModulePath");
                process.Start();

                try
                {
                    if (!AssignProcessToJobObject(job, process.Handle))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    var output = new StreamDrain(process.StandardOutput);
                    var error = new StreamDrain(process.StandardError);

                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        job.Dispose();

                        if (!process.WaitForExit(5000))
                            throw new IOException("The timed-out probe did not terminate.");

                        throw new TimeoutException("PowerShell probe exceeded its time limit.");
                    }

                    job.Dispose();

                    if (!output.Wait(5000) || !error.Wait(5000))
                        throw new IOException("Probe output did not close.");

                    return new Result(process.ExitCode, output.Text + error.Text);
                }
                finally
                {
                    job.Dispose();

                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
            }
        }

        // Drains one redirected stream on a dedicated thread, bounding what it keeps.
        //
        // The thread pool cannot be used for this. Process redirects through synchronous
        // pipes, so an async read there is a blocking read dispatched to a pool thread and
        // it occupies that thread for the whole run, while the caller blocks on another one
        // waiting for it. Several probes in parallel exhaust the pool, and it injects
        // replacement threads about once a second, so the capture wait expired on loaded CI
        // runners although the probe had already exited. A dedicated thread does not queue
        // behind anything.
        private sealed class StreamDrain
        {
            private const int Limit = 131072;

            private readonly StreamReader reader;
            private readonly Thread worker;
            private string captured = "";

            public StreamDrain(StreamReader streamReader)
            {
                reader = streamReader;
                worker = new Thread(Drain);
                worker.IsBackground = true;
                worker.Name = "probe-stream-drain";
                worker.Start();
            }

            /// <summary>Valid once Wait has returned true; the join publishes it to the caller.</summary>
            public string Text
            {
                get { return captured; }
            }

            public bool Wait(int milliseconds)
            {
                return worker.Join(milliseconds);
            }

            private void Drain()
            {
                var text = new StringBuilder();

                try
                {
                    var buffer = new char[4096];
                    int count;

                    while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                        text.Append(buffer, 0, Math.Min(count, Limit - text.Length));
                }
                catch (Exception)
                {
                    // A broken pipe ends the capture. Whatever arrived stays reportable, and
                    // an unhandled exception here would take the whole test host down.
                }

                captured = text.ToString();
            }
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
                {
                    slashes++;
                    continue;
                }

                text.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character);
                slashes = 0;
            }

            return text.Append('\\', slashes * 2).Append('"').ToString();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimit
        {
            public long ProcessTime,
                JobTime;
            public uint Flags;
            public UIntPtr MinimumWorkingSet,
                MaximumWorkingSet;
            public uint ActiveProcesses;
            public UIntPtr Affinity;
            public uint Priority,
                SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimit
        {
            public BasicLimit Basic;
            public ulong ReadOperations,
                WriteOperations,
                OtherOperations,
                ReadBytes,
                WriteBytes,
                OtherBytes;
            public UIntPtr ProcessMemory,
                JobMemory,
                PeakProcessMemory,
                PeakJobMemory;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr attributes, IntPtr name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeFileHandle job,
            int informationClass,
            ref ExtendedLimit limits,
            uint size
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
