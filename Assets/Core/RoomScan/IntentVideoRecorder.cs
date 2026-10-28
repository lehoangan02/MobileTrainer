using System;
using System.IO;
using UnityEngine;

/// <summary>Records through the system camera app (Android) or a file picker (Editor).</summary>
public class IntentVideoRecorder : IVideoRecorder
{
    private readonly Action<Action> postToMainThread;

    public IntentVideoRecorder(Action<Action> postToMainThread)
    {
        this.postToMainThread = postToMainThread;
    }

    public void Record(string destPath, Action<RecordResult> done)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var plugin = new AndroidJavaClass("com.rtr.nativeplugin.RtrNative"))
            {
                plugin.CallStatic("recordVideo", activity, destPath, new CaptureCallback(postToMainThread, done));
            }
        }
        catch (Exception e)
        {
            done(RecordResult.Error(e.Message));
        }
#elif UNITY_EDITOR
        string picked = UnityEditor.EditorUtility.OpenFilePanel("Pick a video to act as the recording", "", "mp4,mov,m4v");
        if (string.IsNullOrEmpty(picked)) { done(RecordResult.Cancelled()); return; }
        try
        {
            File.Copy(picked, destPath, true);
            done(RecordResult.Ok(destPath));
        }
        catch (Exception e)
        {
            done(RecordResult.Error(e.Message));
        }
#else
        done(RecordResult.Error("Recording is only supported on Android."));
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private class CaptureCallback : AndroidJavaProxy
    {
        private readonly Action<Action> post;
        private readonly Action<RecordResult> done;

        public CaptureCallback(Action<Action> post, Action<RecordResult> done)
            : base("com.rtr.nativeplugin.RtrNative$Callback")
        {
            this.post = post;
            this.done = done;
        }

        // Called on the Android UI thread.
        public void onResult(string status, string path, string message)
        {
            RecordResult r;
            switch (status)
            {
                case "ok": r = RecordResult.Ok(path); break;
                case "cancelled": r = RecordResult.Cancelled(); break;
                default: r = RecordResult.Error(message); break;
            }
            post(() => done(r));
        }
    }
#endif
}
