using System;
using System.IO;
using AdNoctem.Substrate.IO;
using Xunit;

namespace AdNoctem.Substrate.Office.Tests;

public sealed class OfficeRecoveryTests
{
    [Fact]
    public void RetiredJournalIsRefusedBeforeReadingEvidence()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "substrate-retired-" + Guid.NewGuid().ToString("N")
        );
        var runId = "77777777777777777777777777777777";
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, runId + ".abandoned.json"), "{}");
            var error = Assert.Throws<OfficeException>(() =>
                new OfficeRecoveryManager().Read(runId, FileSystemPath.Parse(root))
            );
            Assert.Equal(OfficeFailureReason.RetiredRecoveryRecord, error.Reason);
            Assert.Contains(runId, error.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void InformationalRecoveryBlocksDoNotClaimRecoveryIsRequired()
    {
        var request = new OfficeDeploymentRequest(
            OfficeDeploymentAction.Migrate,
            new OfficeConfiguration(
                OfficeProduct.Standard2019Volume,
                version: new Version("16.0.10417.20095")
            ),
            @"C:\Media",
            new[] { "Standard2019Volume" }
        );
        var checkpoint = new OfficeRecoveryCheckpoint(
            1,
            request,
            "StageMedia",
            false,
            new[] { "Standard2019Volume" },
            Array.Empty<string>()
        );
        var policy = new OfficeRecoveryPolicy();
        var busy = policy.Evaluate(
            checkpoint,
            request.Action,
            new[] { "Standard2019Volume" },
            Array.Empty<string>(),
            false,
            false,
            true,
            false,
            true
        );
        Assert.Equal("DeploymentBusy", busy.ReasonCode);
        Assert.False(busy.RecoveryRequired);
        var reboot = policy.Evaluate(
            checkpoint,
            request.Action,
            new[] { "Standard2019Volume" },
            Array.Empty<string>(),
            false,
            false,
            false,
            true,
            true
        );
        Assert.Equal("RebootRequired", reboot.ReasonCode);
        Assert.Equal(3010, reboot.WrapperExitCode);
        Assert.False(reboot.RecoveryRequired);
        var conflict = policy.Evaluate(
            checkpoint,
            request.Action,
            new[] { "VisioPro2019Volume" },
            Array.Empty<string>(),
            false,
            false,
            false,
            false,
            true
        );
        Assert.Equal("Conflict", conflict.ReasonCode);
        Assert.False(conflict.RecoveryRequired);
    }

    [Fact]
    public void ContinuationRequiresKnownScopeAndAnAcceptedCheckpoint()
    {
        var target = new OfficeConfiguration(
            OfficeProduct.Standard2019Volume,
            version: new Version("16.0.10417.20095")
        );
        var request = new OfficeDeploymentRequest(
            OfficeDeploymentAction.Migrate,
            target,
            @"C:\Media",
            new[] { "Standard2019Volume" },
            true
        );
        var checkpoint = new OfficeRecoveryCheckpoint(
            1,
            request,
            "Remove",
            true,
            new[] { "Standard2019Volume" },
            new[] { "synthetic-msi" }
        );
        var policy = new OfficeRecoveryPolicy();
        var result = policy.Evaluate(
            checkpoint,
            request.Action,
            Array.Empty<string>(),
            new[] { "synthetic-msi" },
            false,
            false,
            false,
            false,
            true
        );
        Assert.Equal(OfficeRecoveryDisposition.Continue, result.Disposition);
        Assert.Empty(result.RemainingRemovalIds);
        var partial = new OfficeRecoveryCheckpoint(
            1,
            request,
            "Migrate",
            false,
            checkpoint.PreviousProductIds,
            checkpoint.PreviousMsiProductCodes
        );
        result = policy.Evaluate(
            partial,
            request.Action,
            new[] { "Standard2019Volume" },
            Array.Empty<string>(),
            false,
            false,
            false,
            false,
            true
        );
        Assert.Equal("UnsupportedRecoveryState", result.ReasonCode);
        Assert.True(result.RecoveryRequired);
        Assert.Equal(
            "Conflict",
            policy
                .Evaluate(
                    checkpoint,
                    request.Action,
                    new[] { "VisioPro2019Volume" },
                    Array.Empty<string>(),
                    false,
                    false,
                    false,
                    false,
                    true
                )
                .ReasonCode
        );
        Assert.Equal(
            "DeploymentBusy",
            policy
                .Evaluate(
                    checkpoint,
                    request.Action,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    false,
                    true,
                    true,
                    true,
                    false
                )
                .ReasonCode
        );
        Assert.Equal(
            3010,
            policy
                .Evaluate(
                    checkpoint,
                    request.Action,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    false,
                    true,
                    false,
                    true,
                    false
                )
                .WrapperExitCode
        );
        Assert.Equal(
            OfficeRecoveryDisposition.VerifyOnly,
            policy
                .Evaluate(
                    partial,
                    request.Action,
                    new[] { "Standard2019Volume" },
                    Array.Empty<string>(),
                    false,
                    true,
                    false,
                    false,
                    true
                )
                .Disposition
        );
        Assert.Equal(
            OfficeFailureReason.StaleMedia,
            Assert
                .Throws<OfficeException>(() =>
                    policy.Evaluate(
                        checkpoint,
                        request.Action,
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        false,
                        true,
                        false,
                        false,
                        false
                    )
                )
                .Reason
        );
        Assert.Throws<OfficeException>(() =>
            policy.Evaluate(
                checkpoint,
                OfficeDeploymentAction.Install,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                false,
                false,
                false,
                true
            )
        );
        Assert.False(new OfficeHostPlatform(1, 19044, 9).Supported);
        Assert.True(new OfficeHostPlatform(1, 19045, 9).Supported);
        Assert.False(new OfficeHostPlatform(3, 26100, 9).Supported);
    }
}
