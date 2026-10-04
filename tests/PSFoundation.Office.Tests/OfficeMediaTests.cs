using System;
using System.Linq;
using System.Security;
using Xunit;

namespace PSFoundation.Office.Tests;

public sealed class OfficeMediaTests
{
    [Fact]
    public void ManifestRoundTripAndPayloadValidationKeepPinnedMediaAndLanguagesBound()
    {
        var target = new OfficeConfiguration(OfficeProduct.Standard2019Volume, languages: new[] { "de-de" }, version: new Version("16.0.10417.20095"));
        var package = new OfficeConfiguration(target.Product, languages: new[] { "de-de", "en-us" }, version: target.Version);
        var files = new[]
        {
            File("Office/Data/v64_16.0.10417.20095.cab"),
            File("Office/Data/16.0.10417.20095/stream.x64.x-none.dat"),
            File("Office/Data/16.0.10417.20095/stream.x64.de-de.dat"),
            File("Office/Data/16.0.10417.20095/stream.x64.en-us.dat")
        };
        var manifest = new OfficeMediaManifest(package, "16.0.20326.20112", files);
        var json = manifest.ToJson();
        var parsed = OfficeMediaManifest.Parse(json);
        Assert.Equal(json, parsed.ToJson());
        var validator = new OfficeMediaValidator();
        validator.Validate(parsed, files, target);
        var generic = new[] { File("Office/Data/v64.cab") }.Concat(files.Skip(1)).ToArray();
        validator.Validate(new OfficeMediaManifest(package, manifest.ToolVersion, generic), generic, target);
        var missingLanguage = files.Take(3).ToArray();
        Assert.Equal(OfficeFailureReason.MissingLanguageMedia, Assert.Throws<OfficeException>(() => validator.Validate(new OfficeMediaManifest(package, manifest.ToolVersion, missingLanguage), missingLanguage, target)).Reason);
        var extraLanguage = new OfficeConfiguration(target.Product, languages: new[] { "fr-fr" }, version: target.Version);
        Assert.Equal(OfficeFailureReason.MissingLanguageMedia, Assert.Throws<OfficeException>(() => validator.Validate(parsed, files, extraLanguage)).Reason);
        var altered = new[] { new OfficeMediaFile(files[0].Path, files[0].Length + 1, files[0].Hash) }.Concat(files.Skip(1));
        Assert.Equal(OfficeFailureReason.MediaIntegrityFailed, Assert.Throws<OfficeException>(() => validator.Validate(parsed, altered, target)).Reason);
        Assert.Equal(OfficeFailureReason.MediaMismatch, Assert.Throws<OfficeException>(() => validator.Validate(parsed, files, target, "16.0.010417.20095")).Reason);
        Assert.Equal(OfficeFailureReason.InvalidContract, Assert.Throws<OfficeException>(() => OfficeMediaManifest.Parse(json.Replace("\"SchemaVersion\":2", "\"SchemaVersion\":2,\"schemaversion\":2"))).Reason);
        Assert.Throws<OfficeException>(() => OfficeMediaManifest.Parse(json.Insert(1, "\"__type\":\"Untrusted\",")));
        Assert.Throws<OfficeException>(() => File("Office/Data/../setup.exe"));
        Assert.Throws<OfficeException>(() => new OfficeMediaManifest(package, manifest.ToolVersion, files.Concat(new[] { files[0] })));
        using (var key = new SecureString())
        {
            foreach (var character in " abcde-fghij-klmno-pqrst-uvwxy ")
                key.AppendChar(character);
            OdtTool.ValidateProductKey(key);
            Assert.Equal(31, key.Length);
            key.AppendChar('!');
            var failure = Assert.Throws<OfficeException>(() => OdtTool.ValidateProductKey(key));
            Assert.Equal(OfficeFailureReason.InvalidProductKey, failure.Reason);
            Assert.Equal(OdtTool.ProductKeyFormatMessage, failure.Message);
        }
    }
    private static OfficeMediaFile File(string path) => new OfficeMediaFile(path, 12, new string('a', 64));
}
