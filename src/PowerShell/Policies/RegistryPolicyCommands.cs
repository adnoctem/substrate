using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;
using AdNoctem.Substrate.IO;
using AdNoctem.Substrate.Registry.Policies;

namespace AdNoctem.Substrate.PowerShell.Policies;

[Cmdlet(VerbsData.ConvertFrom, "RegistryPolicy")]
[OutputType(typeof(PSObject))]
public sealed class ConvertFromRegistryPolicyCommand : PSCmdlet, IDisposable
{
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();

    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = "";

    [Parameter]
    public SwitchParameter Raw { get; set; }

    protected override void ProcessRecord()
    {
        var path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        IReadOnlyList<RegistryPolicyRecord> records;

        try
        {
            using (var stream = File.OpenRead(path))
                records = new RegistryPolicyCodec().ReadLegacy(stream, !Raw, stopping.Token);
        }
        catch (RegistryPolicyFormatException error)
        {
            var cause = error.InnerException!;
            var detail = cause.Message;

            if (cause is EndOfStreamException || cause is System.Text.DecoderFallbackException)
                detail =
                    "Exception calling \""
                    + error.ReadOperation
                    + "\" with \""
                    + (error.ReadOperation == "GetString" ? "1" : "0")
                    + "\" argument(s): \""
                    + detail
                    + "\"";
            else if (cause is ArgumentOutOfRangeException)
                detail =
                    "Exception calling \"Substring\" with \"2\" argument(s): \"" + detail + "\"";

            var message =
                "Invalid registry policy '"
                + path
                + "' at byte "
                + error.Offset.ToString(CultureInfo.InvariantCulture)
                + ": "
                + detail;
            ThrowTerminatingError(
                new ErrorRecord(
                    new InvalidDataException(message, cause),
                    message,
                    ErrorCategory.OperationStopped,
                    null
                )
            );

            return;
        }
        catch (IOException error)
        {
            InvocationFailure(error, "OpenRead", 1);

            return;
        }
        catch (UnauthorizedAccessException error)
        {
            InvocationFailure(error, "OpenRead", 1);

            return;
        }

        foreach (var record in records)
        {
            var output = new PSObject();
            output.Properties.Add(new PSNoteProperty("Key", record.Key));
            output.Properties.Add(new PSNoteProperty("ValueName", record.ValueName));
            output.Properties.Add(new PSNoteProperty("Type", record.Type));
            stopping.Token.ThrowIfCancellationRequested();
            output.Properties.Add(
                new PSNoteProperty("Data", Raw ? record.Payload : record.Decode(true))
            );
            output.TypeNames.Insert(
                0,
                Raw ? "PSFoundation.RegistryPolicy.RawEntry" : "PSFoundation.RegistryPolicy.Entry"
            );
            WriteObject(output);
        }
    }

    protected override void EndProcessing() => Dispose();

    protected override void StopProcessing()
    {
        try
        {
            stopping.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    public void Dispose() => stopping.Dispose();

    private void InvocationFailure(Exception error, string method, int arguments)
    {
        var legacy = new MethodInvocationException(
            "Exception calling \""
                + method
                + "\" with \""
                + arguments
                + "\" argument(s): \""
                + error.Message
                + "\"",
            error
        );
        ThrowTerminatingError(
            new ErrorRecord(legacy, error.GetType().Name, ErrorCategory.NotSpecified, null)
        );
    }
}

[Cmdlet(VerbsData.ConvertTo, "RegistryPolicy", SupportsShouldProcess = true)]
[OutputType(typeof(void))]
public sealed class ConvertToRegistryPolicyCommand : PSCmdlet, IDisposable
{
    private readonly List<object?> input = new List<object?>();
    private readonly CancellationTokenSource stopping = new CancellationTokenSource();

    [Parameter(ValueFromPipeline = true, Position = 0), AllowEmptyCollection]
    public object[]? InputObject { get; set; } = Array.Empty<object>();

    [Parameter(Mandatory = true, Position = 1)]
    public string Path { get; set; } = "";

    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        if (InputObject != null)
            input.AddRange(InputObject);
    }

    protected override void EndProcessing()
    {
        try
        {
            var path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);

            if (File.Exists(path) && !Force)
            {
                Fail("Destination '" + path + "' exists. Use -Force to replace it.");

                return;
            }

            var records = new List<RegistryPolicyRecord>();

            foreach (var value in input)
            {
                stopping.Token.ThrowIfCancellationRequested();

                if (value == null)
                {
                    Fail("Policy records must not be null.");

                    return;
                }

                var entry = PSObject.AsPSObject(value);

                if (entry.BaseObject is IDictionary dictionary)
                {
                    entry = new PSObject();

                    foreach (DictionaryEntry pair in dictionary)
                        entry.Properties.Add(
                            new PSNoteProperty(
                                LanguagePrimitives.ConvertTo<string>(pair.Key),
                                pair.Value
                            )
                        );
                }

                foreach (var name in new[] { "Key", "ValueName", "Type", "Data" })
                    if (entry.Properties[name] == null)
                    {
                        Fail("Policy record is missing '" + name + "'.");

                        return;
                    }

                var key = Unwrap(entry.Properties["Key"].Value);
                var nameValue = Unwrap(entry.Properties["ValueName"].Value);

                if (!(key is string keyText))
                {
                    Fail("Policy Key must be a nonempty path relative to the registry hive.");

                    return;
                }

                if (!(nameValue is string nameText))
                {
                    Fail("Policy ValueName must be a string (empty is allowed).");

                    return;
                }

                var typeText = LanguagePrimitives.ConvertTo<string>(entry.Properties["Type"].Value);

                if (!Regex.IsMatch(typeText, @"^\d+$"))
                {
                    Fail("Policy Type must be an unsigned integer.");

                    return;
                }

                uint type;

                try
                {
                    type = (uint)
                        LanguagePrimitives.ConvertTo(
                            entry.Properties["Type"].Value,
                            typeof(uint),
                            CultureInfo.InvariantCulture
                        );
                }
                catch (PSInvalidCastException error)
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                            new RuntimeException(error.Message, error),
                            error.ErrorRecord.FullyQualifiedErrorId,
                            ErrorCategory.InvalidArgument,
                            null
                        )
                    );

                    return;
                }

                var data = Unwrap(entry.Properties["Data"].Value);

                try
                {
                    RegistryPolicyRecord.ValidateIdentifiers(keyText, nameText);
                    byte[] payload;

                    if (
                        entry.TypeNames.Any(name =>
                            string.Equals(
                                name,
                                "PSFoundation.RegistryPolicy.RawEntry",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                    )
                    {
                        if (data != null && !(data is byte[]))
                        {
                            Fail("Raw and opaque policy data must be a byte array.");

                            return;
                        }

                        payload = data as byte[] ?? Array.Empty<byte>();
                    }
                    else
                        payload = RegistryPolicyRecord.EncodePayload(type, data);

                    if (payload.Length > RegistryPolicyCodec.MaximumPayloadSize)
                    {
                        Fail("Policy payload exceeds 65535 bytes.");

                        return;
                    }

                    records.Add(new RegistryPolicyRecord(keyText, nameText, type, payload));
                }
                catch (ArgumentException error)
                {
                    Fail(error.Message);

                    return;
                }
                catch (Exception error)
                    when (error is OverflowException || error is FormatException)
                {
                    var legacy = new MethodInvocationException(
                        "Exception calling \"Parse\" with \"1\" argument(s): \""
                            + error.Message
                            + "\"",
                        error
                    );
                    ThrowTerminatingError(
                        new ErrorRecord(
                            legacy,
                            error.GetType().Name,
                            ErrorCategory.NotSpecified,
                            null
                        )
                    );

                    return;
                }
            }

            byte[] bytes;

            try
            {
                bytes = new RegistryPolicyCodec().Encode(records, stopping.Token);
            }
            catch (ArgumentException error)
            {
                Fail(
                    error.Message.Split(
                        new[] { "\r\nParameter name:", " (Parameter '" },
                        StringSplitOptions.None
                    )[0]
                );

                return;
            }

            if (!ShouldProcess(path, "Write registry policy file"))
                return;

            var operation = "Open";
            var argumentCount = 4;

            using (var content = new MemoryStream(bytes, false))
            {
                try
                {
                    new FileManager()
                        .WriteAtomicallyAsync(
                            FileSystemPath.Parse(System.IO.Path.GetFullPath(path)),
                            content,
                            Force,
                            stopping.Token,
                            (method, arguments) =>
                            {
                                operation = method;
                                argumentCount = arguments;
                            }
                        )
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception error)
                    when (error is IOException || error is UnauthorizedAccessException)
                {
                    var legacy = new MethodInvocationException(
                        "Exception calling \""
                            + operation
                            + "\" with \""
                            + argumentCount
                            + "\" argument(s): \""
                            + error.Message
                            + "\"",
                        error
                    );
                    ThrowTerminatingError(
                        new ErrorRecord(
                            legacy,
                            error.GetType().Name,
                            ErrorCategory.NotSpecified,
                            null
                        )
                    );
                }
            }
        }
        finally
        {
            Dispose();
        }
    }

    private static object? Unwrap(object? value) =>
        value is PSObject wrapped ? wrapped.BaseObject : value;

    private void Fail(string message) =>
        ThrowTerminatingError(
            new ErrorRecord(
                new RuntimeException(message),
                message,
                ErrorCategory.OperationStopped,
                message
            )
        );

    protected override void StopProcessing()
    {
        try
        {
            stopping.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    public void Dispose() => stopping.Dispose();
}
