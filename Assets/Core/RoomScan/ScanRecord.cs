using System;

public enum ScanStatus
{
    Local,
    Queued,
    Uploading,
    Processing,
    Ready,
    Failed
}

[Serializable]
public class ScanRecord
{
    public string id;
    public string title;
    public string notes;
    public string createdUtc;      // ISO 8601 round-trip
    public float durationSec;
    public long sizeBytes;
    public string videoFile;       // file name inside the scan folder
    public string thumbFile;       // file name inside the scan folder, may be empty
    public ScanStatus status;
    public float uploadProgress;   // 0..1, meaningful while Uploading
    public string serverJobId;
    public string statusMessage;   // failure reason

    public DateTime CreatedLocal
    {
        get
        {
            return DateTime.TryParse(createdUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t)
                ? t.ToLocalTime()
                : DateTime.Now;
        }
    }
}

[Serializable]
public class ScanIndex
{
    public int version = 1;
    public System.Collections.Generic.List<ScanRecord> items = new System.Collections.Generic.List<ScanRecord>();
}
