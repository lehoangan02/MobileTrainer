using System;
using System.Collections;

/// <summary>
/// Sends a scan to the processing server. Implementations update the record's status/progress
/// and call <paramref name="onChanged"/> after every change worth showing.
/// </summary>
public interface IScanUploader
{
    IEnumerator Upload(ScanRecord record, Action<bool> onChanged);
}
