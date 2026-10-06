using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdNoctem.Substrate.Diagnostics;
using AdNoctem.Substrate.IO;
using Xunit;

namespace AdNoctem.Substrate.Packages.Tests;

public sealed class ProgramExecutionTests
{
    [Fact]
    public async Task PreparedInstallAndUninstallPreserveArgumentsAndNativeExitStatus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (
            directory != null && !File.Exists(Path.Combine(directory.FullName, "Substrate.slnx"))
        )
            directory = directory.Parent;

        var executable = FileSystemPath.Parse(
            Path.Combine(
                directory!.FullName,
                "build",
                "bin",
                "ProcessHost",
                "Release",
                "net48",
                "ProcessHost.exe"
            )
        );
        var manager = new Win32ProgramManager();
        var arguments = new[] { "", "space and quote\"", @"C:\ending space\", "a&b|c" };
        var install = manager.CreateInstallRequest(
            executable,
            new[] { "arguments" }.Concat(arguments)
        );
        var command = string.Join(
            " ",
            new[] { executable.Value }
                .Concat(install.Process.Arguments)
                .Select(ProcessManager.QuoteArgument)
        );
        var uninstall = manager.CreateUninstallRequest(command);
        Assert.Equal(install.Process.Arguments, uninstall.Process.Arguments);
        var result = await manager.ExecuteAsync(uninstall);
        Assert.True(result.Succeeded);
        Assert.False(result.RebootRequired);
        Assert.Equal(
            arguments,
            result
                .Process.StandardOutput.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(line =>
                    Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(4)))
                )
        );
        var reboot = await manager.ExecuteAsync(
            manager.CreateInstallRequest(
                executable,
                new[] { "streams" },
                successExitCodes: new[] { 17 },
                rebootExitCodes: new[] { 17 }
            )
        );
        Assert.True(reboot.Succeeded);
        Assert.True(reboot.RebootRequired);
        Assert.Equal(17, reboot.Process.ExitCode);
    }

    [Fact]
    public void MsiPreparationAndQuietSelectionDoNotExecuteOrAuthorizeAnInteractiveFallback()
    {
        var manager = new Win32ProgramManager();
        var path = Path.Combine(
            Path.GetTempPath(),
            "AdNoctem.Substrate.PowerShell-" + Guid.NewGuid().ToString("N") + ".msi"
        );
        File.WriteAllBytes(path, Array.Empty<byte>());

        try
        {
            var request = manager.CreateInstallRequest(FileSystemPath.Parse(path), new[] { "/qn" });
            Assert.Equal(new[] { "/i", path, "/qn" }, request.Process.Arguments);
            Assert.Equal(
                Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                request.Process.FileName
            );
            Assert.Equal(
                new[] { "/x", "{synthetic}", "/qn" },
                manager.CreateUninstallRequest("msiexec /I {synthetic} /qn").Process.Arguments
            );
            Assert.Equal(
                "/x{synthetic}",
                manager.CreateUninstallRequest("msiexec /I{synthetic}").Process.Arguments[0]
            );
            Assert.Equal(
                UninstallCommandStatus.InteractiveApprovalRequired,
                manager.SelectUninstallCommand("app.exe", null, true, false).Status
            );
            Assert.Equal(
                UninstallCommandStatus.Ready,
                manager.SelectUninstallCommand("app.exe", null, true, true).Status
            );
            Assert.True(
                manager.SelectUninstallCommand("app.exe", "app.exe /quiet", true, false).IsQuiet
            );
            Assert.Equal(
                UninstallCommandStatus.MissingCommand,
                manager.SelectUninstallCommand(null, null, true, true).Status
            );
        }
        finally
        {
            File.Delete(path);
        }
    }
}
