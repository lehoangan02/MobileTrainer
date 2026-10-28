using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Placeholder uploader: fakes Queued -> Uploading -> Processing -> Ready.
/// A scan whose title contains "fail" ends as Failed, so the Retry path can be exercised.
/// </summary>
public class MockScanUploader : IScanUploader
{
    public IEnumerator Upload(ScanRecord r, Action<bool> onChanged)
    {
        r.status = ScanStatus.Queued;
        r.uploadProgress = 0f;
        r.statusMessage = "";
        onChanged(true);
        yield return new WaitForSecondsRealtime(1f);

        r.status = ScanStatus.Uploading;
        onChanged(true);
        const float uploadSeconds = 5f;
        bool willFail = r.title != null && r.title.ToLowerInvariant().Contains("fail");
        for (float t = 0f; t < uploadSeconds;)
        {
            t += Time.unscaledDeltaTime;
            r.uploadProgress = Mathf.Clamp01(t / uploadSeconds);
            if (willFail && r.uploadProgress >= 0.6f)
            {
                r.status = ScanStatus.Failed;
                r.statusMessage = "Connection lost";
                onChanged(true);
                yield break;
            }
            onChanged(false);
            yield return null;
        }

        r.uploadProgress = 1f;
        r.status = ScanStatus.Processing;
        r.serverJobId = "mock-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        onChanged(true);
        yield return new WaitForSecondsRealtime(3f);

        r.status = ScanStatus.Ready;
        onChanged(true);
    }
}
