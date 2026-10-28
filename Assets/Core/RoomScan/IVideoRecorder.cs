using System;

public enum RecordOutcome
{
    Ok,
    Cancelled,
    Error
}

public struct RecordResult
{
    public RecordOutcome outcome;
    public string path;     // recorded file when Ok
    public string message;  // reason when Error

    public static RecordResult Ok(string path) => new RecordResult { outcome = RecordOutcome.Ok, path = path };
    public static RecordResult Cancelled() => new RecordResult { outcome = RecordOutcome.Cancelled };
    public static RecordResult Error(string message) => new RecordResult { outcome = RecordOutcome.Error, message = message };
}

/// <summary>
/// Records one video to destPath. The callback always runs on the Unity main thread.
/// Phase 1: system camera app. A CameraX-based recorder can replace it behind this interface.
/// </summary>
public interface IVideoRecorder
{
    void Record(string destPath, Action<RecordResult> done);
}
