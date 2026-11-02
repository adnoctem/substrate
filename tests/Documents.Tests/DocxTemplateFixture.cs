using System.Collections.Generic;
using System.IO;
using AdNoctem.Substrate.Documents;

namespace AdNoctem.Substrate.Documents.Tests;

internal static class DocxTemplateFixture
{
    internal static byte[] Create(string language = "en")
    {
        var template = new AuthoringTemplate(
            [
                AuthoringField.Text(
                    PressReleaseTags.Headline,
                    "Headline",
                    null,
                    "Base headline placeholder",
                    true
                ),
                AuthoringField.Text(
                    PressReleaseTags.Dateline,
                    "Dateline",
                    null,
                    "Base dateline placeholder",
                    true
                ),
                AuthoringField.Rich(
                    PressReleaseTags.Body,
                    "Body",
                    null,
                    [new ParagraphBlock([new TextInline("Base body placeholder")])],
                    true
                ),
                AuthoringField.Group(
                    PressReleaseTags.Specifications,
                    "Specifications",
                    false,
                    AuthoringField.Text(
                        PressReleaseTags.SpecificationsHeading,
                        "Heading",
                        null,
                        "Base specifications heading"
                    ),
                    AuthoringField.Table(
                        PressReleaseTags.SpecificationsTable,
                        "Rows",
                        ["Label", "Value"],
                        new[]
                        {
                            new IEnumerable<DocumentInline>[]
                            {
                                [new TextInline("Base label:")],
                                [new TextInline("Base value")],
                            },
                        },
                        emptyRows: 0
                    )
                ),
                AuthoringField.Group(
                    PressReleaseTags.FactBox,
                    "Fact box",
                    false,
                    AuthoringField.Text(
                        PressReleaseTags.FactBoxHeading,
                        "Heading",
                        null,
                        "Base fact box heading"
                    ),
                    AuthoringField.Rich(
                        PressReleaseTags.FactBoxBody,
                        "Body",
                        null,
                        [new ParagraphBlock([new TextInline("Base fact box body")])]
                    )
                ),
                AuthoringField.Rich(
                    PressReleaseTags.Media,
                    "Media",
                    null,
                    [new ParagraphBlock([new TextInline("Base media placeholder")])]
                ),
                AuthoringField.Rich(
                    PressReleaseTags.CompanyDescription("example"),
                    "About example",
                    null,
                    [new ParagraphBlock([new TextInline("Base company description")])]
                ),
                AuthoringField.Rich(
                    PressReleaseTags.Editorial,
                    "Editorial",
                    null,
                    [new ParagraphBlock([new TextInline("Base editorial placeholder")])]
                ),
                AuthoringField.Group(
                    PressReleaseTags.Contact,
                    "Contact",
                    true,
                    AuthoringField.Text(
                        PressReleaseTags.ContactHeading,
                        "Heading",
                        null,
                        "Base contact heading",
                        true
                    ),
                    AuthoringField.Rich(
                        PressReleaseTags.ContactDetails,
                        "Details",
                        null,
                        [new ParagraphBlock([new TextInline("Base contact details")])],
                        true
                    )
                ),
            ],
            WordFixture.Metadata(language),
            language == "de" ? "de-DE" : "en-GB",
            WordFixture.Release()
        );

        using (var stream = new MemoryStream())
        {
            new AuthoringTemplateBuilder().Write(template, stream, asTemplate: false);
            return stream.ToArray();
        }
    }
}
