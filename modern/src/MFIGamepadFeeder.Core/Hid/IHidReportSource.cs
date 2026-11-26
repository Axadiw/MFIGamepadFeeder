namespace MFIGamepadFeeder.Core.Hid;

public enum HidReadResultKind
{
    Data,
    Timeout,
    Disconnected,
}

public readonly record struct HidReadResult(HidReadResultKind Kind, int BytesRead, string? ErrorMessage = null);

public interface IHidReportStream : IDisposable
{
    int MaxInputReportLength { get; }
    HidReadResult Read(byte[] buffer, CancellationToken cancellationToken);
}

public interface IHidReportSource
{
    string DisplayName { get; }
    bool TryOpen(out IHidReportStream? stream, out string? errorMessage);
}
