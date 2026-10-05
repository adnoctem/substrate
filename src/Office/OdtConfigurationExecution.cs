using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AdNoctem.Substrate.Diagnostics;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Office;

public sealed partial class OdtTool
{
    public const string ProductKeyFormatMessage = "ProductKey has an invalid format. Supply 25 characters as five groups of five letters or digits separated by ASCII hyphens, for example ABCDE-FGHIJ-KLMNO-PQRST-UVWXY.";
    /// <summary>Writes an operation-owned protected XML file, invokes ODT and removes the file before returning.</summary>
    /// <remarks>The caller retains ownership of the document and optional SecureString. A product key is written only into the temporary
    /// file, never into the supplied document, arguments or result. Cleanup failures retain the native exit code when available.</remarks>
    public async Task<ProcessResult> InvokeConfigurationAsync(FileSystemPath executable, XmlDocument document, FileSystemPath directory,
        OdtMode mode = OdtMode.Configure, SecureString? productKey = null, CancellationToken cancellationToken = default)
    {
        if (executable == null)
            throw new ArgumentNullException(nameof(executable));
        if (directory == null)
            throw new ArgumentNullException(nameof(directory));
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (mode == OdtMode.Help || !Enum.IsDefined(typeof(OdtMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        cancellationToken.ThrowIfCancellationRequested();
        if (document.DocumentType != null || document.DocumentElement?.Name != "Configuration"
            || document.SelectNodes("//@*")!.Cast<XmlAttribute>().Any(attribute => string.Equals(attribute.LocalName, "PIDKEY", StringComparison.OrdinalIgnoreCase)))
            throw new OfficeException(OfficeFailureReason.InvalidAuthority, "Use a Configuration document without DTDs or embedded product keys.");
        // Snapshot before asynchronous work so caller changes cannot alter the operation's selected XML.
        var snapshot = (XmlDocument)document.CloneNode(true);
        snapshot.XmlResolver = null;
        XmlElement? keyedProduct = null;
        if (productKey != null)
        {
            var products = snapshot.SelectNodes("/Configuration/Add/Product")!;
            if (products.Count != 1 || !(products[0] is XmlElement product) || !product.GetAttribute("ID").EndsWith("Volume", StringComparison.OrdinalIgnoreCase))
                throw new OfficeException(OfficeFailureReason.InvalidAuthority, "A product key requires a volume installation configuration.");
            keyedProduct = product;
        }
        char[]? key = null;
        var path = directory.Combine("configuration-" + Guid.NewGuid().ToString("N") + ".xml");
        var created = false;
        Exception? failure = null;
        ProcessResult? result = null;
        try
        {
            if (productKey != null)
                key = ReadProductKey(productKey);
            new OfficePathGuard().RequireProtected(directory.Value, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            using (var output = new FileStream(path.Value, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                {
                    writer.WriteStartDocument();
                    Write(snapshot.DocumentElement!, writer);
                    writer.WriteEndDocument();
                }
                output.Flush(true);
            }
            if (key != null)
            { Array.Clear(key, 0, key.Length); key = null; }
            result = await InvokeAsync(executable, mode, path, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            if (key != null)
                Array.Clear(key, 0, key.Length);
            if (created)
            {
                try
                { new OfficePathGuard().ValidatePath(path.Value); File.Delete(path.Value); }
                catch (Exception cleanup)
                {
                    const string message = "Temporary configuration cleanup failed; inspect the protected working directory.";
                    if (failure != null)
                        failure.Data["OfficeCleanupError"] = message;
                    else
                    {
                        var error = new OfficeException(OfficeFailureReason.ConfigurationCleanupFailed, message, cleanup);
                        if (result != null)
                            error.Data["OfficeExitCode"] = result.ExitCode;
                        throw error;
                    }
                }
            }
        }
        void Write(XmlNode node, XmlWriter writer)
        {
            if (!(node is XmlElement element))
            { node.WriteTo(writer); return; }
            writer.WriteStartElement(element.Prefix, element.LocalName, element.NamespaceURI);
            foreach (XmlAttribute attribute in element.Attributes)
                attribute.WriteTo(writer);
            if (ReferenceEquals(element, keyedProduct))
            { writer.WriteStartAttribute("PIDKEY"); writer.WriteChars(key!, 0, key!.Length); writer.WriteEndAttribute(); }
            foreach (XmlNode child in element.ChildNodes)
                Write(child, writer);
            writer.WriteEndElement();
        }
    }
    /// <summary>Runs a generated configuration using protected temporary files and explicit execution settings.</summary>
    public ProcessResult InvokeConfiguration(FileSystemPath executable, XmlDocument document, FileSystemPath directory,
        OdtMode mode = OdtMode.Configure, SecureString? productKey = null, CancellationToken cancellationToken = default)
        => InvokeConfigurationAsync(executable, document, directory, mode, productKey, cancellationToken).GetAwaiter().GetResult();
    /// <summary>Checks a volume key without returning or logging its plaintext.</summary>
    public static void ValidateProductKey(SecureString productKey)
    { var key = ReadProductKey(productKey); Array.Clear(key, 0, key.Length); }
    private static char[] ReadProductKey(SecureString productKey)
    {
        if (productKey == null)
            throw new ArgumentNullException(nameof(productKey));
        using var snapshot = productKey.Copy();
        if (snapshot.Length < 29 || snapshot.Length > 4096)
            throw new OfficeException(OfficeFailureReason.InvalidProductKey, ProductKeyFormatMessage);
        var temporary = new char[snapshot.Length];
        var result = new char[29];
        var pointer = Marshal.SecureStringToBSTR(snapshot);
        var valid = false;
        try
        {
            Marshal.Copy(pointer, temporary, 0, temporary.Length);
            var first = 0;
            var last = temporary.Length - 1;
            while (first <= last && char.IsWhiteSpace(temporary[first]))
                first++;
            while (last >= first && char.IsWhiteSpace(temporary[last]))
                last--;
            if (last - first + 1 != 29)
                throw new OfficeException(OfficeFailureReason.InvalidProductKey, ProductKeyFormatMessage);
            for (var index = 0; index < result.Length; index++)
            {
                var value = char.ToUpperInvariant(temporary[first + index]);
                if ((index + 1) % 6 == 0 ? value != '-' : !(value >= 'A' && value <= 'Z' || value >= '0' && value <= '9'))
                    throw new OfficeException(OfficeFailureReason.InvalidProductKey, ProductKeyFormatMessage);
                result[index] = value;
            }
            valid = true;
            return result;
        }
        finally
        {
            Array.Clear(temporary, 0, temporary.Length);
            if (!valid)
                Array.Clear(result, 0, result.Length);
            Marshal.ZeroFreeBSTR(pointer);
        }
    }
}
