using System;

public static class ScanFormat
{
    public static string Duration(float sec)
    {
        int s = Math.Max(0, (int)Math.Round(sec));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    public static string Size(long bytes)
    {
        if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0") + " GB";
        if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0") + " MB";
        return Math.Max(1, bytes / 1024) + " KB";
    }

    public static string Date(DateTime local) => local.ToString("d MMM yyyy, HH:mm");

    public static string Status(ScanRecord r)
    {
        switch (r.status)
        {
            case ScanStatus.Local: return "Local";
            case ScanStatus.Queued: return "Queued";
            case ScanStatus.Uploading: return "Uploading " + (int)(r.uploadProgress * 100f) + "%";
            case ScanStatus.Processing: return "Processing";
            case ScanStatus.Ready: return "Ready";
            default: return "Failed";
        }
    }

    /// <summary>Date, length and size, led by the failure reason when an upload failed.</summary>
    public static string Summary(ScanRecord r)
    {
        string dur = r.durationSec > 0f ? Duration(r.durationSec) + "  |  " : "";
        string reason = r.status == ScanStatus.Failed && !string.IsNullOrEmpty(r.statusMessage) ? r.statusMessage + "  |  " : "";
        return reason + Date(r.CreatedLocal) + "  |  " + dur + Size(r.sizeBytes);
    }
}
