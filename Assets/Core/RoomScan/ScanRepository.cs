using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists scan metadata as JSON and owns the per-scan folders:
/// {persistentDataPath}/scans/index.json and /scans/{id}/video.mp4 (+ thumb.jpg).
/// </summary>
public static class ScanRepository
{
    public static event Action Changed;

    private static ScanIndex index;

    public static string RootDir => Path.Combine(Application.persistentDataPath, "scans");
    private static string IndexPath => Path.Combine(RootDir, "index.json");

    public static string ScanDir(string id) => Path.Combine(RootDir, id);
    public static string VideoPath(ScanRecord r) => Path.Combine(ScanDir(r.id), r.videoFile);
    public static string ThumbPath(ScanRecord r) =>
        string.IsNullOrEmpty(r.thumbFile) ? null : Path.Combine(ScanDir(r.id), r.thumbFile);

    public static IReadOnlyList<ScanRecord> All
    {
        get { EnsureLoaded(); return index.items; }
    }

    public static ScanRecord Get(string id)
    {
        EnsureLoaded();
        return index.items.Find(r => r.id == id);
    }

    /// <summary>Allocates a record + folder for a new recording. Not stored until <see cref="Add"/>.</summary>
    public static ScanRecord Create()
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 12);
        Directory.CreateDirectory(ScanDir(id));
        DateTime now = DateTime.Now;
        return new ScanRecord
        {
            id = id,
            title = "Room " + now.ToString("d MMM HH:mm"),
            notes = "",
            createdUtc = now.ToUniversalTime().ToString("o"),
            videoFile = "video.mp4",
            thumbFile = "",
            status = ScanStatus.Local
        };
    }

    public static void Add(ScanRecord r)
    {
        EnsureLoaded();
        index.items.Insert(0, r);
        Save();
    }

    /// <summary>Persists changes made to a record obtained from <see cref="Get"/>.</summary>
    public static void Update(ScanRecord r)
    {
        EnsureLoaded();
        Save();
    }

    /// <summary>Raises <see cref="Changed"/> for transient state (e.g. upload progress) without writing to disk.</summary>
    public static void NotifyTransient()
    {
        Changed?.Invoke();
    }

    public static void Delete(string id)
    {
        EnsureLoaded();
        index.items.RemoveAll(r => r.id == id);
        try
        {
            string dir = ScanDir(id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ScanRepository] Could not delete folder: " + e.Message);
        }
        Save();
    }

    public static void Discard(ScanRecord notAdded)
    {
        try
        {
            string dir = ScanDir(notAdded.id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ScanRepository] Could not discard folder: " + e.Message);
        }
    }

    private static void EnsureLoaded()
    {
        if (index != null) return;
        index = new ScanIndex();
        try
        {
            if (File.Exists(IndexPath))
            {
                var loaded = JsonUtility.FromJson<ScanIndex>(File.ReadAllText(IndexPath));
                if (loaded != null && loaded.items != null) index = loaded;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ScanRepository] Could not read index, starting empty: " + e.Message);
        }

        // Drop entries whose video vanished; fail uploads interrupted by an app restart.
        bool dirty = index.items.RemoveAll(r => !File.Exists(VideoPath(r))) > 0;
        foreach (var r in index.items)
        {
            if (r.status == ScanStatus.Queued || r.status == ScanStatus.Uploading)
            {
                r.status = ScanStatus.Failed;
                r.statusMessage = "Interrupted";
                dirty = true;
            }
        }
        if (dirty) Save();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(RootDir);
            string tmp = IndexPath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(index, true));
            if (File.Exists(IndexPath)) File.Delete(IndexPath);
            File.Move(tmp, IndexPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[ScanRepository] Save failed: " + e.Message);
        }
        Changed?.Invoke();
    }
}
