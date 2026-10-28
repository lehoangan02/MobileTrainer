using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persistent service host for the room-scan feature: recording, post-processing of new recordings,
/// uploads (which must outlive scene changes) and sharing.
/// </summary>
public class ScanServices : MonoBehaviour
{
    private static ScanServices instance;
    public static ScanServices Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("ScanServices");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ScanServices>();
            }
            return instance;
        }
    }

    public IVideoRecorder Recorder { get; set; }
    public IScanUploader Uploader { get; set; }

    private readonly Queue<Action> mainThread = new Queue<Action>();
    private readonly Dictionary<string, Coroutine> uploads = new Dictionary<string, Coroutine>();

    public bool IsRecording { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Recorder = new IntentVideoRecorder(Post);
        Uploader = new MockScanUploader();
    }

    /// <summary>Queues work to run on the Unity main thread; safe to call from any thread.</summary>
    public void Post(Action a)
    {
        lock (mainThread) mainThread.Enqueue(a);
    }

    private void Update()
    {
        while (true)
        {
            Action a;
            lock (mainThread)
            {
                if (mainThread.Count == 0) break;
                a = mainThread.Dequeue();
            }
            try { a(); } catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>Records a new scan and adds it to the repository. result: the new record, or null with error (null error = cancelled).</summary>
    public void RecordNew(Action<ScanRecord, string> result)
    {
        if (IsRecording) return;
        IsRecording = true;
        ScanRecord rec = ScanRepository.Create();
        string dest = ScanRepository.VideoPath(rec);

        Recorder.Record(dest, r =>
        {
            IsRecording = false;
            if (r.outcome != RecordOutcome.Ok)
            {
                ScanRepository.Discard(rec);
                result(null, r.outcome == RecordOutcome.Error ? (r.message ?? "Recording failed") : null);
                return;
            }
            FinalizeRecording(rec);
            ScanRepository.Add(rec);
            result(rec, null);
        });
    }

    private static void FinalizeRecording(ScanRecord rec)
    {
        string video = ScanRepository.VideoPath(rec);
        try { rec.sizeBytes = new FileInfo(video).Length; } catch { rec.sizeBytes = 0; }

        string thumb = Path.Combine(ScanRepository.ScanDir(rec.id), "thumb.jpg");
        NativeBridge.VideoInfo info = NativeBridge.ProbeVideo(video, thumb);
        if (info.valid) rec.durationSec = info.durationSec;
        if (File.Exists(thumb)) rec.thumbFile = "thumb.jpg";
    }

    public void StartUpload(ScanRecord r)
    {
        if (uploads.ContainsKey(r.id)) return;
        uploads[r.id] = StartCoroutine(RunUpload(r));
    }

    public bool IsUploading(string id) => uploads.ContainsKey(id);

    private System.Collections.IEnumerator RunUpload(ScanRecord r)
    {
        yield return Uploader.Upload(r, persist =>
        {
            if (persist) ScanRepository.Update(r); else ScanRepository.NotifyTransient();
        });
        uploads.Remove(r.id);
        ScanRepository.Update(r);
    }

    public void DeleteScan(string id)
    {
        if (uploads.TryGetValue(id, out Coroutine c))
        {
            StopCoroutine(c);
            uploads.Remove(id);
        }
        ScanRepository.Delete(id);
    }

    public bool Share(ScanRecord r)
    {
        return NativeBridge.ShareVideo(ScanRepository.VideoPath(r), r.title);
    }
}
