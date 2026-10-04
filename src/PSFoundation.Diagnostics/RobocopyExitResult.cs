using System;

namespace PSFoundation.Diagnostics;

[Flags]
public enum RobocopyExitFlags { None = 0, Copied = 1, ExtraFiles = 2, Mismatches = 4, CopyFailures = 8, FatalError = 16 }

/// <summary>Decodes Robocopy's result without executing the vendor tool.</summary>
public sealed class RobocopyExitResult
{
    private static readonly string[] Descriptions = { "No Change", "OKCOPY", "XTRA", "OKCOPY + XTRA", "MISMATCHES",
        "OKCOPY + MISMATCHES", "MISMATCHES + XTRA", "OKCOPY + MISMATCHES + XTRA", "FAIL", "OKCOPY + FAIL",
        "FAIL + XTRA", "OKCOPY + FAIL + XTRA", "FAIL + MISMATCHES", "OKCOPY + FAIL + MISMATCHES",
        "FAIL + MISMATCHES + XTRA", "OKCOPY + FAIL + MISMATCHES + XTRA", "***FATAL ERROR***" };
    public int ExitCode { get; }
    public RobocopyExitFlags Flags => (RobocopyExitFlags)ExitCode;
    public bool IsSuccess => ExitCode >= 0 && ExitCode < 8;
    public string Description => ExitCode >= 0 && ExitCode < Descriptions.Length ? Descriptions[ExitCode] : "Unknown";
    public RobocopyExitResult(int exitCode) { ExitCode = exitCode; }
}
