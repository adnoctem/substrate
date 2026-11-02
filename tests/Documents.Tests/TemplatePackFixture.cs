using System;
using System.IO;
using System.Linq;
using System.Text;
using AdNoctem.Substrate.IO;

namespace AdNoctem.Substrate.Documents.Tests;

internal sealed class TemplatePackFixture : IDisposable
{
    internal const string DefaultHtml = """
        <!doctype html>
        <html lang="{{language}}"><head><meta charset="UTF-8"><title>{{title}}</title></head>
        <body><header>{{masthead}}</header>
        <h1>{{headline}}</h1>{{body}}
        {{#specifications}}<h2>{{specifications.heading}}</h2><ul>{{specifications.rows}}</ul>{{/specifications}}
        {{#factbox}}<h2>{{factbox.heading}}</h2>{{factbox.body}}{{/factbox}}
        {{#media}}<div class="media-link">{{media}}</div>{{/media}}
        {{#about}}<div class="about-box">{{about}}</div>{{/about}}
        {{#editorial}}<div class="editorial">{{editorial}}</div>{{/editorial}}
        <footer><div>{{contact.heading}}</div><div>{{contact.details}}</div></footer>
        </body></html>
        """;

    internal const string AllSections =
        "[\"specifications\",\"factBox\",\"media\",\"companyDescriptions\",\"editorial\",\"contact\"]";

    internal string Root { get; }
    internal FileSystemPath Path => FileSystemPath.Parse(Root);

    internal TemplatePackFixture()
    {
        Root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "substrate-pack-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(Root);
        WriteBytes("base.docx", DocxTemplateFixture.Create());
        Write("release.en.html", DefaultHtml);
        Write("release.de.html", DefaultHtml);
        WriteManifest();
    }

    internal TemplatePack Load() => TemplatePack.Load(Path);

    internal void Write(string relative, string text) =>
        WriteBytes(relative, new UTF8Encoding(false).GetBytes(text));

    internal void WriteBytes(string relative, byte[] bytes) =>
        File.WriteAllBytes(System.IO.Path.Combine(Root, relative), bytes);

    internal void WriteManifest(
        string schema = "press-release/1",
        string schemaVersions = "[\"1.0\"]",
        string? companyDescriptions = "[\"example\"]",
        string enHtml = "release.en.html",
        string deHtml = "release.de.html",
        string docx = "base.docx",
        string enSections = AllSections,
        string deSections = AllSections,
        string enMasthead = "PRESS RELEASE",
        string deMasthead = "PRESSEMITTEILUNG"
    ) =>
        Write(
            "pack.json",
            "{"
                + "\"packVersion\":\"1.0\","
                + "\"schema\":\""
                + Json(schema)
                + "\","
                + "\"schemaVersions\":"
                + schemaVersions
                + ","
                + "\"outputLayoutVersion\":\"1.0\","
                + "\"companyDescriptions\":"
                + (companyDescriptions ?? "null")
                + ","
                + "\"languages\":["
                + Language("en", enMasthead, enHtml, docx, enSections)
                + ","
                + Language("de", deMasthead, deHtml, docx, deSections)
                + "]"
                + "}"
        );

    internal string Hash(string relative)
    {
        var path = System.IO.Path.Combine(Root, relative);

        if (!File.Exists(path))
            return new string('0', 64);

        using (var sha = System.Security.Cryptography.SHA256.Create())
            return string.Concat(
                sha.ComputeHash(File.ReadAllBytes(path)).Select(item => item.ToString("x2"))
            );
    }

    private string Language(
        string code,
        string masthead,
        string html,
        string docx,
        string sections
    ) =>
        "{\"code\":\""
        + Json(code)
        + "\",\"masthead\":\""
        + Json(masthead)
        + "\",\"datelineSeparator\":\" – \",\"specificationSeparator\":\" \","
        + "\"html\":{\"path\":\""
        + Json(html)
        + "\",\"sha256\":\""
        + Hash(html)
        + "\"},"
        + "\"docx\":{\"path\":\""
        + Json(docx)
        + "\",\"sha256\":\""
        + Hash(docx)
        + "\"},"
        + "\"sections\":"
        + sections
        + "}";

    private static string Json(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public void Dispose() => Directory.Delete(Root, true);
}
