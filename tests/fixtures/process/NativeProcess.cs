using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

public static class NativeProcess
{
    public static int Main(string[] args)
    {
        switch (args[0])
        {
            case "bounded-memory":
                // Initialize console/encoding before exhausting the job's memory allowance.
                Console.WriteLine("MemoryLimitProbeStarted");
                var allocations = new System.Collections.Generic.List<byte[]>();

                try
                {
                    // Deliberately finite even if the runner's memory limit is broken.
                    for (int i = 0; i < 256; i++)
                        allocations.Add(new byte[1024 * 1024]);

                    GC.KeepAlive(allocations);

                    return 0;
                }
                catch (OutOfMemoryException)
                {
                    // Release the pressure before allocating output/exception infrastructure.
                    // Reporting failure must not itself fail nondeterministically with another OOM.
                    allocations.Clear();
                    GC.Collect();
                    Console.WriteLine("MemoryLimitReached");

                    return 23;
                }
            case "/g":
            case "/t":
                Console.WriteLine(args[0]);

                for (int i = 1; i < args.Length; i++)
                    Console.WriteLine(
                        "ARG:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(args[i]))
                    );

                if (System.IO.File.Exists(args[1]) && System.IO.File.ReadAllText(args[1]) == "fail")
                {
                    Console.Error.WriteLine("Synthetic failure");

                    return 7;
                }

                return 0;
            case "short-wait":
                Thread.Sleep(1000);
                Console.WriteLine("finished");

                return 0;
            case "environment":
                Console.WriteLine(Environment.CurrentDirectory);
                Console.WriteLine(Environment.GetEnvironmentVariable("SUBSTRATE_SYNTHETIC_VALUE"));

                return 0;
            case "arguments":
                for (int i = 1; i < args.Length; i++)
                    Console.WriteLine(
                        "ARG:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(args[i]))
                    );

                return 0;
            case "streams":
                for (int i = 0; i < 512; i++)
                {
                    Console.Out.Write(new string('O', 1024));
                    Console.Error.Write(new string('E', 1024));
                }

                return 17;
            case "wait":
                Console.WriteLine(Process.GetCurrentProcess().Id);
                Console.Out.Flush();
                SignalReady(args);
                Thread.Sleep(60000);

                return 0;
            case "tree":
                using (
                    Process child = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            Arguments = "sleep",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        }
                    )
                )
                {
                    Console.WriteLine(Process.GetCurrentProcess().Id);
                    Console.WriteLine(child.Id);
                    Console.Out.Flush();
                    SignalReady(args);
                    Thread.Sleep(60000);
                }

                return 0;
            case "sleep":
                Thread.Sleep(60000);

                return 0;
            default:
                return 2;
        }
    }

    private static void SignalReady(string[] args)
    {
        if (args.Length > 1)
            using (var ready = EventWaitHandle.OpenExisting(args[1]))
                ready.Set();
    }
}
