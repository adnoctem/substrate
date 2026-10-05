# Using the registry library

This guide illustrates common workflows. The generated [API catalog](API.md) links to the complete member reference.

Reference `PSFoundation.Registry` directly; no PowerShell host or dependency-injection container is required. The library targets net48 and
netstandard2.0 and performs Windows registry operations. The repository builds with the pinned SDK through `tools/tasks.proj`.

## Ordinary operations

```csharp
using Microsoft.Win32;
using PSFoundation.Registry;

var registry = new RegistryManager(RegistryView.Registry64);
var settings = RegistryPath.Parse(@"HKCU\Software\ExampleApplication");

registry.CreateKey(settings);
registry.SetValue(settings, "Enabled", RegistryValue.DWord(1));
registry.SetValue(settings, "DataPath", RegistryValue.ExpandString(@"%LOCALAPPDATA%\ExampleApplication"));

if (registry.TryGetValue(settings, "Enabled", out var value))
{
    int enabled = value!.GetData<int>();
}

string? rawPath = registry.GetValue(settings, "DataPath")?.GetString();
string? expandedPath = registry.GetValue(settings, "DataPath")?.GetString(expandEnvironmentVariables: true);
```

The manager fixes the machine and view for every operation. `RegistryView.Default` resolves to process bitness at construction. Paths use
native hive names or abbreviations, and are literal: `*`, `?`, `.` and `..` are not pattern/navigation operators. C# paths do not accept
PowerShell provider syntax. Path identity is case-insensitive; string value data is case-sensitive.

An empty value name denotes the default value. `(default)` is an ordinary literal name in C#. Missing key/value reads return null;
`TryGetValue` returns false. `ValueExists` checks names, including values with unsupported native kinds. Access errors propagate rather than
becoming absence. `GetValues` and `GetSubKeys` throw for a missing parent. `OpenKey` returns a caller-owned `RegistryKey`; use `using`.

`RegistryValue` supports String, ExpandString, DWord, QWord, Binary, MultiString and None. Explicit kinds require matching CLR types.
Integer data uses `int`/`long`; unsigned factory overloads preserve the same bits. Binary/multi-string arrays are defensively copied. String
data cannot contain NUL, and multi-string elements cannot be empty; an empty multi-string array is supported. Unsupported native kinds are
reported as unsupported, never as missing. Malformed data fails explicitly. Environment expansion is opt-in and uses the calling process's
environment even for remotely read values.

## Capabilities and choices

| API                                                               | Contract                                                                                                                                  |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `CreateKey`, `DeleteKey`                                          | Missing ancestors can be created. Root hive creation/deletion is refused. Recursive deletion is explicit.                                 |
| `GetValue`, `TryGetValue`, `SetValue`, `DeleteValue`, `GetValues` | Typed values; `SetValue` requires an existing key unless `createKey: true`.                                                               |
| `GetSubKeys`, `EnumerateKeys`                                     | Ordered traversal, root at depth zero, optional depth limit and cancellation.                                                             |
| `Search`, `SearchAsync`                                           | Literal substring matching of selected names/data; structured matches, not a formatted report. Binary data is hexadecimal.                |
| `RenameKey`                                                       | Native rename within a parent; no destination overwrite.                                                                                  |
| `CopyKey`, `CopyKeyAsync`                                         | Data/empty-key copy; destination policy is FailIfExists, Merge or Replace. Permissions are inherited, not copied.                         |
| `MoveKey`, `MoveKeyAsync`                                         | Copy to a missing destination, then remove the unchanged source. Returns separate copy/removal outcomes. Permissions are not transferred. |
| `Snapshots`                                                       | Value/subtree capture, pure comparison, reviewable restore plans, value restoration and partial-completion results.                       |
| `Files`                                                           | Local `.reg` import/export. Export replaces existing files only with explicit overwrite and successful output.                            |
| `Hives`                                                           | Local binary hive save/restore and owned mount leases. Distinct from `.reg` files.                                                        |
| `Security`                                                        | Read/write explicitly selected security descriptor parts, binary or SDDL. No automatic privilege enabling.                                |
| `Watch`                                                           | Disposable local change notification source, with cancellable asynchronous waits.                                                         |

Copy/move reject overlapping trees. Copying preserves neither security descriptors nor a point-in-time view of a concurrently changing
source. Use `RenameKey` for an in-place rename. There are no automatic retries that overwrite newer state, implicit recursive deletion or
registry transactions.

## Snapshots and restoration

```csharp
var before = await registry.Snapshots.CaptureTreeAsync(settings, cancellationToken);

// After application-controlled changes, inspect exactly what restoring would do.
var plan = registry.Snapshots.PlanRestore(before, RegistryRestoreMode.Merge, cancellationToken);
foreach (var change in plan.Changes)
{
    // Present or record typed Path, Action, Name, Before and After as appropriate to the caller.
}

var result = await registry.Snapshots.ApplyAsync(plan, cancellationToken);
if (result.Status != RegistryApplyStatus.Completed)
{
    // Completed lists the writes already performed. IncompleteChange and Error explain the stop.
}
```

Snapshots include raw values and empty keys, with a resolved view and machine identity. They do not include permissions. Capture is not
transactional; callers requiring quiescence must arrange it. `At` explicitly maps a captured tree to another location/view/machine without
performing I/O. Pure `RegistrySnapshotService.Compare` compares snapshots at the same location.

Merge preserves unmentioned data. Replace deletes unmentioned values/keys inside the selected root. Plans are immutable and apply only to
their original machine/view. Apply first compares the entire captured baseline, then rechecks each operation immediately before writing and
verifies afterward. These checks reduce races; they cannot remove the gap between a check and a native write. New data encountered during
key deletion causes a conflict instead of recursive pruning.

`RestoreValue` touches only its named value and can check an explicit expected snapshot. Absence deletes that value without deleting the
key. Restoring a present value may recreate its parent; use tree plans when each key creation needs a separate change record.

Apply reports Completed, Conflict, Failed or Cancelled and retains completed writes; it does not roll them back. A write is recorded before
verification, so failed verification does not imply that nothing changed. Read-only cancellation and cancellation during the capture phase
of copy/move throw `OperationCanceledException`. Once applying a plan, cancellation returns a partial result. An optional `IProgress` gets
verified completed changes; applications choose whether its delivery is synchronous or dispatched.

## Asynchronous work and lifetime

```csharp
using (var watcher = registry.Watch(settings, includeSubKeys: true))
{
    await watcher.WaitForChangeAsync(cancellationToken);
    // Re-read the relevant settings. Notifications do not identify individual writes.
}
```

Watchers arm immediately, allow one waiter at a time, rearm after a signal and keep working after a cancelled wait. Dispose unregisters the
wait, closes the key/event, and cancels a pending wait. Notifications can coalesce, and deletion invalidates the watched key: recreate the
watcher after reopening a replacement key. Remote notifications are unsupported. The thread-agnostic notification flag requires Windows 8 or
later; this capability does not change the support requirements of ordinary registry operations.

Search, capture, copy/move and apply async methods offload synchronous native traversal to the thread pool and check cancellation between
operations. They do not make remote RPC or an individual synchronous registry call interruptible. Simple native CRUD remains synchronous.
Process-backed operations and notifications use asynchronous waits without requiring a caller synchronization context.

## Files, hives and remote access

File/hive operations use the Windows system `reg.exe` matching the selected view, with independent argument quoting and captured output.
Import also supplies an explicit view flag. Export/save publish through a sibling temporary file, preserve the old destination on failure,
and default to refusing overwrite. Relative filesystem paths use the process working directory; applications needing stable resolution
should pass absolute paths. `RegistryFileService` accepts an optional execution timeout.

Import consumes the whole file, which may target multiple hives or remove keys. It is not constrained to a subtree and can leave partial
changes on failure/cancellation. Binary restore can also leave partial changes. `RegistryCommandException` retains native exit code and
output for the caller; the library does not log them. No executable download or elevation occurs.

`Hives.Mount`/`MountAsync` require a missing immediate child of HKLM or HKU and return a `RegistryHiveLease`. Dispose unloads only the mount
acquired by that lease. Close all opened child handles first. Unload failure throws and permits retry; no forced GC is performed. Once a
load starts it finishes before ownership is returned, even if cancellation is requested meanwhile, so callers cannot abandon a successful
load without receiving its lease. Cancellation is checked before starting. `Unmount` is the explicit operation for caller-managed mounts;
the caller is responsible for ownership and avoiding external replacement/unload races. Save/restore/load privileges remain a caller and
environment concern.

`new RegistryManager(RegistryView.Registry64, "server-name")` uses native remote registry access under the caller's credentials. Remote
operations are deliberately limited to HKLM and HKU; use an explicit SID for user data. Network reachability, Remote Registry service and
permissions are not configured automatically. File/hive operations and notifications require a local manager, rather than silently acting on
the local machine when a remote manager was supplied.

## Verification boundaries

Tests exercise the public API directly under net48 and net10.0-windows, using synthetic disposable HKCU keys. Coverage includes all
supported value kinds, views, traversal/search, copy/move/rename, snapshot conflicts and partial cancellation, permissions/denial,
notifications and real `.reg` export/import. Hive ownership/failure paths use a fake runner. Existing PowerShell contract/behavior
comparisons remain in both real hosts.

Actual elevated binary hive save/restore/load/unload, remote-host integration, SACL/owner-changing privileges, clean runtime-only machines,
native .NET Framework 4.8 and other architectures still require prepared validation environments. This checkpoint makes no claim that these
environment-dependent gates have run. The public API is still an unreleased v2 design; separate NuGet distribution is not introduced here.

Native behavior references:
[registry notifications](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regnotifychangekeyvalue),
[rename](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regrenamekey),
[security descriptors](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-reggetkeysecurity),
[remote registry](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registrykey.openremotebasekey), and
[reg import](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/reg-import).
