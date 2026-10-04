using System;

namespace PSFoundation.Policies;

public sealed class LgpoSourceAvailability
{
    public Uri Source { get; }
    public bool Available => Error == null;
    public int? StatusCode { get; }
    public long? ContentLength { get; }
    public Exception? Error { get; }
    public DateTime CheckedAtUtc { get; }
    internal LgpoSourceAvailability(Uri source, int? statusCode, long? contentLength, Exception? error)
    { Source = source; StatusCode = statusCode; ContentLength = contentLength; Error = error; CheckedAtUtc = DateTime.UtcNow; }
}
