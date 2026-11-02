using System;

namespace AdNoctem.Substrate.Documents;

/// <summary>Stable classifications for template-pack and writer failures.</summary>
public enum DocumentFailureReason
{
    InvalidTemplatePack,
    UnsupportedTemplateVersion,
    UnsupportedLanguage,
    UnsupportedSection,
    UnsafeTemplatePath,
    MissingTemplateAsset,
    TemplateAssetHashMismatch,
    InvalidTemplate,
    InvalidRelease,
    InvalidPackage,
    WriterFailed,
}

/// <summary>A document-generation failure caused by an invalid pack, template or release contract.</summary>
/// <remarks>Caller argument faults use standard argument exceptions, and filesystem faults remain IO exceptions. This
/// exception identifies failures that callers may report without inspecting messages.</remarks>
public sealed class DocumentException : InvalidOperationException
{
    public DocumentFailureReason Reason { get; }

    public DocumentException(
        DocumentFailureReason reason,
        string message,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        Reason = reason;
        Data["DocumentReason"] = reason.ToString();
    }
}
