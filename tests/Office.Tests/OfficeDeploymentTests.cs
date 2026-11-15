using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Registry;
using Microsoft.Win32;
using Xunit;

namespace AdNoctem.Substrate.Office.Tests;

public sealed class OfficeDeploymentTests
{
    private const string Machine = "11111111-2222-3333-4444-555555555555";
    private const string Product = "Standard2019Volume";
    private static readonly OfficeConfiguration Target = new OfficeConfiguration(
        OfficeProduct.Standard2019Volume,
        languages: new[] { "de-de" },
        version: new Version("16.0.10417.20095")
    );
    private static readonly OfficeDeploymentOptions Options = new OfficeDeploymentOptions(
        FileSystemPath.Parse(@"C:\Synthetic\setup.exe"),
        FileSystemPath.Parse(@"C:\Synthetic\Logs")
    );

    [Fact]
    public async Task MigrationRechecksScopeJournalsProgressAndResumesOnlyProvenRemoval()
    {
        var runtime = new Runtime();
        var manager = new OfficeDeploymentManager(runtime);
        var plan = manager.GetPlan(
            new OfficeDeploymentRequest(
                OfficeDeploymentAction.Migrate,
                Target,
                @"C:\Synthetic\Media",
                new[] { Product }
            )
        );
        Assert.True(
            plan.Eligible,
            string.Join(", ", plan.Blockers) + "; " + string.Join(", ", plan.Warnings)
        );
        Assert.Equal(
            OfficeDeploymentStatus.Preview,
            manager.Preview(plan, OfficeDeploymentAction.Migrate, Options).Status
        );
        Assert.Empty(runtime.Journals);

        using (var key = new SecureString())
        {
            foreach (var character in "abcde-fghij-klmno-pqrst-uvwxy")
                key.AppendChar(character);

            var result = await manager.MigrateAsync(plan, Options, key);
            Assert.Equal(OfficeDeploymentStatus.Completed, result.Status);
            Assert.True(result.Verification!.Compliant);
            Assert.Equal(
                new[] { OfficeDeploymentAction.Remove, OfficeDeploymentAction.Migrate },
                runtime.Phases
            );
            Assert.Equal(new[] { false, true }, runtime.Keys);
            Assert.True(runtime.Cleaned && runtime.Released);
            Assert.False(result.RecoveryRequired);
            Assert.All(runtime.Journals, json => Assert.DoesNotContain("abcde", json));
            var final = OfficeRecoveryRecord.Parse(runtime.Journals.Last(), result.RunId, Machine);
            Assert.Equal("Verify", final.Phase);
            Assert.True(final.PhaseCompleted);
            var noOp = manager.Migrate(manager.GetPlan(plan.Request), Options);
            Assert.True(noOp.AlreadyCompliant);
        }

        runtime = new Runtime { RemoveExit = 3010 };
        manager = new OfficeDeploymentManager(runtime);
        plan = manager.GetPlan(plan.Request);
        var stopped = manager.Migrate(plan, Options);
        Assert.Equal(OfficeDeploymentStatus.Failed, stopped.Status);
        Assert.Equal("RebootRequired", stopped.ReasonCode);
        Assert.True(stopped.RecoveryRequired && stopped.RebootRequired);
        Assert.Equal(OfficeDeploymentAction.Remove, Assert.Single(runtime.Phases));
        runtime.Record = OfficeRecoveryRecord.Parse(
            runtime.Journals.Last(),
            stopped.RunId,
            Machine
        );
        runtime.PendingReboot = true;
        Assert.Equal(
            3010,
            manager
                .PreviewRecovery(stopped.RunId, OfficeDeploymentAction.Migrate, Options)
                .WrapperExitCode
        );
        runtime.PendingReboot = false;
        runtime.Phases.Clear();
        var resumed = manager.ResumeMigration(stopped.RunId, Options);
        Assert.Equal(OfficeDeploymentStatus.Completed, resumed.Status);
        Assert.Equal(OfficeDeploymentAction.Migrate, Assert.Single(runtime.Phases));
        Assert.Empty(resumed.Plan.Request.RemoveProductIds);
        Assert.NotEqual(stopped.RunId, resumed.RunId);

        runtime = new Runtime();
        manager = new OfficeDeploymentManager(runtime);
        plan = manager.GetPlan(plan.Request);
        runtime.Current = Inventory(null);
        Assert.Equal(
            OfficeFailureReason.StalePlan,
            Assert.Throws<OfficeException>(() => manager.Migrate(plan, Options)).Reason
        );
        Assert.Empty(runtime.Journals);
    }

    [Fact]
    public void FailuresCancellationAndTamperedEvidenceNeverClaimSuccessOrReplay()
    {
        var runtime = new Runtime { FinalExit = 1603, CleanupFails = true };
        var manager = new OfficeDeploymentManager(runtime);
        var request = new OfficeDeploymentRequest(
            OfficeDeploymentAction.Migrate,
            Target,
            @"C:\Synthetic\Media",
            new[] { Product }
        );
        var result = manager.Migrate(manager.GetPlan(request), Options);
        Assert.Equal(OfficeDeploymentStatus.Failed, result.Status);
        Assert.Null(result.Changed);
        Assert.False(result.ChangeKnown);
        Assert.True(result.RecoveryRequired && runtime.Released);
        Assert.Single(result.Residue);
        var json = runtime.Journals.Last();
        runtime.Record = OfficeRecoveryRecord.Parse(json, result.RunId, Machine);
        Assert.Equal(
            "UnsupportedRecoveryState",
            manager
                .PreviewRecovery(result.RunId, OfficeDeploymentAction.Migrate, Options)
                .ReasonCode
        );
        Assert.Equal(
            OfficeFailureReason.InvalidAuthority,
            Assert
                .Throws<OfficeException>(() => manager.ResumeInstallation(result.RunId, Options))
                .Reason
        );
        Assert.Throws<OfficeException>(() =>
            OfficeRecoveryRecord.Parse(
                json.Replace("\"Settings\":{}", "\"Settings\":{\"Unexpected\":true}"),
                result.RunId,
                Machine
            )
        );
        Assert.Throws<OfficeException>(() =>
            OfficeRecoveryRecord.Parse(
                json.Replace("\"InventoryFingerprint\":\"", "\"InventoryFingerprint\":\"bad"),
                result.RunId,
                Machine
            )
        );
        Assert.Throws<OfficeException>(() =>
            OfficeRecoveryRecord.Parse(
                json.Insert(1, "\"__type\":\"Untrusted\","),
                result.RunId,
                Machine
            )
        );
        var pilot = OfficeDocument.Parse(json);
        pilot["SchemaVersion"] = 2;
        pilot.Object("Plan")["SchemaVersion"] = 2;
        pilot.Object("Plan")["PilotMigration"] = true;
        pilot.Object("Plan")["PilotResources"] = OfficeDocument.Create(
            "UiLanguages",
            Array.Empty<string>(),
            "PrimaryLanguage",
            "de-de",
            "ProofingLanguages",
            Array.Empty<string>(),
            "Provisioning",
            null,
            "Verification",
            null
        );
        runtime.Record = OfficeRecoveryRecord.Parse(pilot.ToJson(), result.RunId, Machine);
        Assert.Equal(
            OfficeFailureReason.UnsupportedPilotRecovery,
            Assert
                .Throws<OfficeException>(() => manager.ResumeMigration(result.RunId, Options))
                .Reason
        );

        using (var cancellation = new CancellationTokenSource())
        {
            runtime = new Runtime { CancelAfterRemove = cancellation };
            manager = new OfficeDeploymentManager(runtime);
            var cancelled = manager.Migrate(
                manager.GetPlan(request),
                Options,
                cancellationToken: cancellation.Token
            );
            Assert.Equal("Cancelled", cancelled.ReasonCode);
            Assert.Single(runtime.Phases);
            Assert.True(runtime.Cleaned && runtime.Released && cancelled.RecoveryRequired);
            var checkpoint = OfficeRecoveryRecord.Parse(
                runtime.Journals.Last(),
                cancelled.RunId,
                Machine
            );
            Assert.Equal("Remove", checkpoint.Phase);
            Assert.True(checkpoint.PhaseCompleted);
        }
    }

    [Fact]
    public void VerificationUnknownsNameFieldsAndDoNotClaimRecovery()
    {
        var runtime = new Runtime { Current = Inventory("x64", RegistryValue.DWord(1)) };
        var manager = new OfficeDeploymentManager(runtime);
        var plan = manager.GetPlan(
            new OfficeDeploymentRequest(
                OfficeDeploymentAction.Install,
                Target,
                @"C:\Synthetic\Media"
            )
        );
        Assert.Equal(OfficeDeploymentState.Incomplete, plan.State);
        Assert.Contains("VerificationUnknown", plan.Blockers);
        Assert.DoesNotContain("RecoveryRequired", plan.Blockers);
        Assert.Contains(
            plan.Warnings,
            warning => warning == "Verification cannot establish: ExcludeApp"
        );
        var blocked = manager.Preview(plan, OfficeDeploymentAction.Install, Options);
        Assert.Equal(OfficeDeploymentStatus.Blocked, blocked.Status);
        Assert.Equal("VerificationUnknown", blocked.ReasonCode);
        Assert.False(blocked.RecoveryRequired);
    }

    [Fact]
    public void SafeRecoveryDivergenceContinuesAndRecordsEvidence()
    {
        var runId = "11111111111111111111111111111111";
        var runtime = new Runtime { Current = Inventory("x86") };
        var manager = new OfficeDeploymentManager(runtime);
        var plan = manager.GetPlan(
            new OfficeDeploymentRequest(
                OfficeDeploymentAction.Install,
                Target,
                @"C:\Synthetic\Media"
            )
        );
        runtime.Record = RecoveryRecord(
            plan,
            runId,
            "StageMedia",
            false,
            OfficeDocument.Create("After", OfficeInventorySerializer.Document(Inventory("x86")))
        );
        runtime.Current = Inventory(null);
        var preview = manager.PreviewRecovery(runId, OfficeDeploymentAction.Install, Options);
        Assert.Equal(OfficeDeploymentStatus.Preview, preview.Status);
        Assert.NotNull(preview.RecoveryDivergence);
        Assert.True(preview.RecoveryDivergence!.DiffersFromBefore);
        Assert.True(preview.RecoveryDivergence.HasRecordedAfter);
        Assert.True(preview.RecoveryDivergence.DiffersFromAfter);
        Assert.Contains("Products", preview.RecoveryDivergence.ChangedFieldsSinceBefore);
        Assert.Contains("Products", preview.RecoveryDivergence.ChangedFieldsSinceAfter);
        Assert.Empty(runtime.Phases);
        var resumed = manager.ResumeInstallation(runId, Options);
        Assert.Equal(OfficeDeploymentStatus.Completed, resumed.Status);
        Assert.Equal(OfficeDeploymentAction.Install, Assert.Single(runtime.Phases));
        Assert.NotNull(resumed.RecoveryDivergence);
        Assert.True(resumed.RecoveryDivergence!.DiffersFromBefore);
    }

    [Fact]
    public void IneligibleRefreshedRecoveryPlanBlocksAsStaleRecoveryState()
    {
        var runId = "22222222222222222222222222222222";
        var runtime = new Runtime { Current = Inventory("x86") };
        var manager = new OfficeDeploymentManager(runtime);
        var plan = manager.GetPlan(
            new OfficeDeploymentRequest(
                OfficeDeploymentAction.Install,
                Target,
                @"C:\Synthetic\Media"
            )
        );
        runtime.Record = RecoveryRecord(plan, runId, "StageMedia", false);
        runtime.Current = Inventory("x64", RegistryValue.DWord(1));
        var result = manager.ResumeInstallation(runId, Options);
        Assert.Equal(OfficeDeploymentStatus.Blocked, result.Status);
        Assert.Equal("StaleRecoveryState", result.ReasonCode);
        Assert.False(result.RecoveryRequired);
        Assert.NotNull(result.RecoveryDivergence);
        Assert.True(result.RecoveryDivergence!.DiffersFromBefore);
        Assert.Contains("Products", result.RecoveryDivergence.ChangedFieldsSinceBefore);
        Assert.Contains("VerificationUnknown", result.Error);
        Assert.Empty(runtime.Phases);
    }

    [Fact]
    public void VerifyPhaseRecoveryRemainsBlockedWithoutExecution()
    {
        var runId = "33333333333333333333333333333333";
        var runtime = new Runtime { Current = Inventory("x86") };
        var manager = new OfficeDeploymentManager(runtime);
        var plan = manager.GetPlan(
            new OfficeDeploymentRequest(
                OfficeDeploymentAction.Install,
                Target,
                @"C:\Synthetic\Media"
            )
        );
        runtime.Record = RecoveryRecord(plan, runId, "Verify", false);
        runtime.Current = Inventory("x64", RegistryValue.DWord(1));
        var result = manager.ResumeInstallation(runId, Options);
        Assert.Equal(OfficeDeploymentStatus.Blocked, result.Status);
        Assert.Equal("UnsupportedRecoveryState", result.ReasonCode);
        Assert.True(result.RecoveryRequired);
        Assert.NotNull(result.RecoveryDivergence);
        Assert.Empty(runtime.Phases);
    }

    [Fact]
    public void RetirementValidatesAndArchivesJournalEvidence()
    {
        var runId = "44444444444444444444444444444444";
        var runtime = new Runtime { Current = Inventory("x64") };
        var manager = new OfficeDeploymentManager(runtime);
        var plan = manager.GetPlan(
            new OfficeDeploymentRequest(
                OfficeDeploymentAction.Install,
                Target,
                @"C:\Synthetic\Media"
            )
        );
        runtime.Record = RecoveryRecord(plan, runId, "StageMedia", false);
        var result = manager.RetireRecovery(
            runId,
            "Synthetic stale journal",
            Options.LogRoot,
            executionMetadataJson: "{\"Caller\":\"synthetic\"}"
        );
        Assert.Equal(runId, result.RunId);
        Assert.Equal(Options.LogRoot.Combine(runId + ".abandoned.json").Value, result.AuditPath);
        Assert.Equal(Options.LogRoot.Combine("Abandoned").Value, result.ArchivePath);
        Assert.Equal(
            Options.LogRoot.Combine("Abandoned").Combine(runId + ".json").Value,
            Assert.Single(result.ArchivedPaths)
        );
        Assert.Equal(
            OfficeInventorySerializer.GetFingerprint(runtime.Current),
            result.CurrentInventoryFingerprint
        );
        var audit = OfficeDocument.Parse(Assert.Single(runtime.RetirementAuditJson));
        Assert.Equal(runId, audit.Text("RunId"));
        Assert.Equal(Machine, audit.Text("MachineId"));
        Assert.Equal("Synthetic stale journal", audit.Text("Reason"));
        Assert.Equal(result.CurrentInventoryFingerprint, audit.Text("InventoryFingerprint"));
        Assert.Equal("synthetic", audit.Object("Execution").Text("Caller"));
        Assert.Equal(runId, Assert.Single(runtime.ArchivedRuns));
        Assert.Empty(runtime.Phases);
    }

    [Fact]
    public void RetirementRejectsInvalidAuthorityBeforeWriting()
    {
        var runId = "55555555555555555555555555555555";
        var runtime = new Runtime { Current = Inventory("x86") };
        var manager = new OfficeDeploymentManager(runtime);
        var plan = manager.GetPlan(
            new OfficeDeploymentRequest(
                OfficeDeploymentAction.Install,
                Target,
                @"C:\Synthetic\Media"
            )
        );
        runtime.Record = RecoveryRecord(plan, runId, "StageMedia", false);
        runtime.Current = Inventory("x64", machineId: "66666666-7777-8888-9999-000000000000");
        var error = Assert.Throws<OfficeException>(() =>
            manager.RetireRecovery(runId, "Synthetic reason", Options.LogRoot)
        );
        Assert.Equal(OfficeFailureReason.InvalidRecoveryRecord, error.Reason);
        Assert.Empty(runtime.RetirementAuditJson);
        Assert.Empty(runtime.ArchivedRuns);
        Assert.Throws<ArgumentException>(() => manager.RetireRecovery(runId, " ", Options.LogRoot));
        Assert.Throws<ArgumentException>(() =>
            manager.RetireRecovery("not-a-run", "reason", Options.LogRoot)
        );
        Assert.Empty(runtime.RetirementAuditJson);
        Assert.Empty(runtime.ArchivedRuns);
    }

    private static OfficeInventory Inventory(
        string? architecture,
        RegistryValue? exclusionValue = null,
        string machineId = Machine
    )
    {
        var records = new List<OfficeRegistryRecord>();

        if (architecture != null)
        {
            var root = @"HKLM\SOFTWARE\Microsoft\Office\ClickToRun\";
            var configuration = new Dictionary<string, RegistryValue>
            {
                ["ProductReleaseIds"] = RegistryValue.String(Product),
                ["Platform"] = RegistryValue.String(architecture),
                ["CDNBaseUrl"] = RegistryValue.String(
                    "https://officecdn.microsoft.com/pr/f2e724c1-748f-4b47-8fb8-8e0d210e9208"
                ),
                ["ClientCulture"] = RegistryValue.String("de-de"),
                [Product + ".ExcludedApps"] = exclusionValue ?? RegistryValue.String(""),
            };

            records.Add(
                new OfficeRegistryRecord(
                    RegistryView.Registry64,
                    RegistryPath.Parse(root + "Configuration"),
                    configuration
                )
            );
            records.Add(
                new OfficeRegistryRecord(
                    RegistryView.Registry64,
                    RegistryPath.Parse(root + @"Inventory\Office\16.0"),
                    new Dictionary<string, RegistryValue>
                    {
                        ["OfficeProductReleaseIds"] = RegistryValue.String(Product),
                        ["OfficePackageVersion"] = RegistryValue.String(Target.Version!.ToString()),
                    }
                )
            );
            records.Add(
                new OfficeRegistryRecord(
                    RegistryView.Registry64,
                    RegistryPath.Parse(root + "ProductReleaseIDs"),
                    new Dictionary<string, RegistryValue>
                    {
                        ["ActiveConfiguration"] = RegistryValue.String(Machine),
                    },
                    new[] { Machine }
                )
            );
            var productPath = root + @"ProductReleaseIDs\" + Machine + "\\" + Product + ".16";
            records.Add(
                new OfficeRegistryRecord(
                    RegistryView.Registry64,
                    RegistryPath.Parse(productPath),
                    new Dictionary<string, RegistryValue>(),
                    new[] { "de-de", "x-none" }
                )
            );

            foreach (var language in new[] { "de-de", "x-none" })
                records.Add(
                    new OfficeRegistryRecord(
                        RegistryView.Registry64,
                        RegistryPath.Parse(productPath + "\\" + language),
                        new Dictionary<string, RegistryValue>
                        {
                            ["Version"] = RegistryValue.String(Target.Version!.ToString()),
                        }
                    )
                );
        }

        return new OfficeInventoryManager().Analyze(machineId, records);
    }

    private static OfficeRecoveryRecord RecoveryRecord(
        OfficeDeploymentPlanDocument plan,
        string runId,
        string phase,
        bool phaseCompleted,
        OfficeDocument? result = null
    )
    {
        var now = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        var journal = OfficeDocument.Create(
            "SchemaVersion",
            1,
            "RunId",
            runId,
            "MachineId",
            Machine,
            "Action",
            plan.Request.Action.ToString(),
            "Plan",
            plan.Data,
            "ConfigurationFingerprint",
            OfficeDocument.Fingerprint(plan.Data["Configuration"]),
            "MediaFingerprint",
            plan.MediaFingerprint,
            "Phase",
            phase,
            "PhaseCompleted",
            phaseCompleted,
            "NativeResults",
            Array.Empty<object>(),
            "RebootRequired",
            false,
            "CreatedAt",
            now,
            "UpdatedAt",
            now,
            "Result",
            result
        );

        return OfficeRecoveryRecord.Parse(journal.ToJson(), runId, Machine);
    }

    private sealed class Runtime : OfficeDeploymentRuntime
    {
        internal OfficeInventory Current = OfficeDeploymentTests.Inventory("x86");
        internal readonly List<string> Journals = new List<string>();
        internal readonly List<OfficeDeploymentAction> Phases = new List<OfficeDeploymentAction>();
        internal readonly List<bool> Keys = new List<bool>();
        internal readonly List<FileSystemPath> RetirementAuditPaths = new List<FileSystemPath>();
        internal readonly List<string> RetirementAuditJson = new List<string>();
        internal readonly List<string> ArchivedRuns = new List<string>();
        internal int RemoveExit,
            FinalExit;
        internal bool Cleaned,
            Released,
            CleanupFails,
            PendingReboot;
        internal OfficeRecoveryRecord? Record;
        internal CancellationTokenSource? CancelAfterRemove;

        internal override OfficeInventory Inventory(CancellationToken cancellation) => Current;

        internal override OfficeMediaAssessment Media(
            OfficeDeploymentPlanDocument plan,
            CancellationToken cancellation
        )
        {
            var files = new[]
            {
                new OfficeMediaFile(
                    "Office/Data/v64_16.0.10417.20095.cab",
                    12,
                    new string('a', 64)
                ),
            };
            var manifest = new OfficeMediaManifest(Target, "16.0.20326.20112", files);

            return new OfficeMediaAssessment(
                FileSystemPath.Parse(@"C:\Synthetic\Media"),
                "Verified",
                manifest,
                manifest.ToJson(),
                files,
                null
            );
        }

        internal override OfficeActivationState Activation(
            OfficeProduct product,
            CancellationToken cancellation
        ) => OfficeActivationState.Licensed;

        internal override void RequireReady(
            OfficeDeploymentOptions options,
            CancellationToken cancellation
        )
        {
            cancellation.ThrowIfCancellationRequested();
        }

        internal override IDisposable AcquireLock(CancellationToken cancellation) =>
            new Lease(() => Released = true);

        internal override void CreateWork(
            FileSystemPath root,
            FileSystemPath work,
            CancellationToken cancellation
        ) { }

        internal override void WriteJournal(FileSystemPath path, string json)
        {
            var document = OfficeDocument.Parse(json);
            _ = OfficeRecoveryRecord.Parse(json, document.Text("RunId")!, Machine);
            Journals.Add(json);
        }

        internal override void WriteLog(FileSystemPath path, string json)
        {
            Assert.NotNull(OfficeDocument.Parse(json).Object("Execution"));
        }

        internal override void WriteRecoveryRetirement(
            FileSystemPath path,
            string json,
            CancellationToken cancellation
        )
        {
            RetirementAuditPaths.Add(path);
            RetirementAuditJson.Add(json);
        }

        internal override IReadOnlyList<string> ArchiveRecoveryArtifacts(
            FileSystemPath logRoot,
            string runId,
            CancellationToken cancellation
        )
        {
            ArchivedRuns.Add(runId);

            return new[] { logRoot.Combine("Abandoned").Combine(runId + ".json").Value };
        }

        internal override void Stage(
            OfficeDeploymentPlanDocument plan,
            OfficeDeploymentOptions options,
            FileSystemPath work,
            CancellationToken cancellation
        ) { }

        internal override void CloseApplications(
            bool authorized,
            Action closed,
            CancellationToken cancellation
        ) { }

        internal override int RunPhase(
            OfficeDeploymentPlanDocument plan,
            OfficeDeploymentAction action,
            FileSystemPath work,
            SecureString? key,
            CancellationToken cancellation
        )
        {
            Phases.Add(action);
            Keys.Add(key != null);

            if (action == OfficeDeploymentAction.Remove)
            {
                Current = OfficeDeploymentTests.Inventory(null);
                CancelAfterRemove?.Cancel();

                return RemoveExit;
            }

            if (FinalExit == 0)
                Current = OfficeDeploymentTests.Inventory("x64");

            return FinalExit;
        }

        internal override void Cleanup(FileSystemPath work, FileSystemPath root)
        {
            Cleaned = true;

            if (CleanupFails)
                throw new InvalidOperationException("Synthetic cleanup failure.");
        }

        internal override OfficeRecoveryRecord ReadRecovery(
            string runId,
            FileSystemPath logRoot,
            CancellationToken cancellation
        ) => Record!;

        internal override bool DeploymentBusy(CancellationToken cancellation) => false;

        internal override bool RebootPending(CancellationToken cancellation) => PendingReboot;
    }

    private sealed class Lease : IDisposable
    {
        private readonly Action release;

        internal Lease(Action release)
        {
            this.release = release;
        }

        public void Dispose() => release();
    }
}
