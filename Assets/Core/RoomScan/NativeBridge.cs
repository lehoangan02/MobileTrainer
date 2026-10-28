using UnityEngine;

/// <summary>Thin wrapper over the Android plugin for share and video probing; no-ops elsewhere.</summary>
public static class NativeBridge
{
    public struct VideoInfo
    {
        public float durationSec;
        public int width;
        public int height;
        public bool valid;
    }

    public static VideoInfo ProbeVideo(string videoPath, string thumbPath)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var plugin = new AndroidJavaClass("com.rtr.nativeplugin.RtrNative"))
            {
                string res = plugin.CallStatic<string>("probeVideo", videoPath, thumbPath);
                if (!string.IsNullOrEmpty(res))
                {
                    string[] p = res.Split('|');
                    int.TryParse(p[0], out int ms);
                    int w = 0, h = 0;
                    if (p.Length > 2) { int.TryParse(p[1], out w); int.TryParse(p[2], out h); }
                    return new VideoInfo { durationSec = ms / 1000f, width = w, height = h, valid = true };
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[NativeBridge] ProbeVideo failed: " + e.Message);
        }
#endif
        return default;
    }

    public static bool ShareVideo(string videoPath, string title)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var plugin = new AndroidJavaClass("com.rtr.nativeplugin.RtrNative"))
            {
                plugin.CallStatic("shareVideo", activity, videoPath, title);
                return true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[NativeBridge] ShareVideo failed: " + e.Message);
            return false;
        }
#elif UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(videoPath);
        return true;
#else
        return false;
#endif
    }
}
