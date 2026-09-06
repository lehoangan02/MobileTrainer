using UnityEngine;
using UnityEngine.Animations;     // AnimationClipPlayable
using UnityEngine.Playables;      // AnimationPlayableUtilities lives HERE, and the SetTime/SetSpeed extensions

/// Plays recorded tutorial clips on the ghost rig.
///
/// Placement:
///   DroneAnchored - rigRoot is a CHILD of DroneAnchorRoot_new, so its localPosition is the
///                   offset from the real drone and it tracks the drone through QR respawns
///                   for free. This component must NOT touch the transform in that mode.
///   HeadYaw       - legacy diorama behaviour: freeze rigRoot on the learner's head-yaw frame.
public class TutorialPlayer : MonoBehaviour
{
    public enum PlacementMode { DroneAnchored, HeadYaw }

    [Header("Refs")]
    public Transform rigRoot;         // TutorialRigRoot
    public Animator animator;         // the Animator on TutorialRigRoot
    public AnimationClip clip;        // fallback clip; per-step clips come via PlayClip()
    public Transform headAnchor;      // CenterEyeAnchor

    [Header("Placement")]
    [Tooltip("DroneAnchored: rigRoot's parent puts it beside the real drone; this script leaves the " +
             "transform alone. HeadYaw: place it in front of the learner after placeDelay.")]
    public PlacementMode placement = PlacementMode.DroneAnchored;
    [Tooltip("HeadYaw only: seconds to wait after launch before freezing the station.")]
    public float placeDelay = 2f;
    [Tooltip("HeadYaw only: offset in the head-yaw frame.")]
    public Vector3 stationOffset = Vector3.zero;

    [Header("Playback")]
    [Tooltip("Play the fallback 'clip' on Start. Leave OFF when TutorialDirector drives the clips.")]
    public bool autoPlay = true;
    public bool loop = true;
    [Range(0.1f, 2f)] public float speed = 1f;

    [Header("Trim (fallback clip only; clipEnd <= 0 means 'to the end')")]
    public float clipStart = 6f;
    public float clipEnd = 16f;

    double _t0, _t1;

    PlayableGraph _graph;
    AnimationClipPlayable _playable;
    bool _ready, _playing;
    AnimationClip _current;

    public bool IsPlaying { get { return _playing; } }
    public bool Looping   { get { return loop; } }
    public AnimationClip CurrentClip { get { return _current; } }
    public float Progress01 { get { return (_ready && _t1 > _t0) ? (float)((_playable.GetTime() - _t0) / (_t1 - _t0)) : 0f; } }

    System.Collections.IEnumerator Start()
    {
        if (placement == PlacementMode.HeadYaw)
        {
            float t = 0f;
            while (t < placeDelay) { t += Time.deltaTime; yield return null; }
            PlaceAtHead();
        }
        // DroneAnchored: the parent transform already places us beside the drone. Do nothing.

        if (autoPlay && clip != null)
        {
            PlayClip(clip, clipStart, clipEnd);
        }
    }

    /// Freeze the station on the learner's yaw-only head frame (HeadYaw mode only).
    public void PlaceAtHead()
    {
        if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
        if (headAnchor == null || rigRoot == null) { Debug.LogError("[TUT] player: rigRoot/headAnchor not set"); return; }

        Vector3 fwd = headAnchor.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        Quaternion yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);

        rigRoot.SetPositionAndRotation(headAnchor.position + yaw * stationOffset, yaw);
        Debug.Log($"[TUT] station placed at {rigRoot.position:F3}");
    }

    // ---- per-step clip control (TutorialDirector calls these) ---------------

    /// Swap in a clip and start it from the beginning. Pass trim seconds to play a sub-range;
    /// per-step clips are already trimmed by recording, so the defaults play the whole thing.
    public void PlayClip(AnimationClip c, float trimStart = 0f, float trimEnd = 0f)
    {
        if (c == null) { StopClip(); return; }
        if (animator == null) { Debug.LogError("[TUT] player: animator not set"); return; }

        DestroyGraph();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _playable = AnimationPlayableUtilities.PlayClip(animator, c, out _graph);
        _current = c;

        _t0 = Mathf.Max(0f, trimStart);
        _t1 = (trimEnd > 0f && trimEnd < c.length) ? trimEnd : c.length;
        if (_t1 <= _t0) _t1 = c.length;

        _playable.SetTime(_t0);
        _playable.SetSpeed(0);
        _ready = true;
        Play();
        Debug.Log($"[TUT] clip '{c.name}' {_t0:F2}-{_t1:F2}s");
    }

    /// Stop and tear down playback, leaving the ghosts wherever they are.
    public void StopClip()
    {
        Pause();
        DestroyGraph();
        _ready = false;
        _current = null;
    }

    void DestroyGraph()
    {
        if (_graph.IsValid()) _graph.Destroy();
        _ready = false;
    }

    void Update()
    {
        if (!_ready || !_playing) return;
        if (_playable.GetTime() >= _t1)
        {
            if (loop) _playable.SetTime(_t0);
            else { _playable.SetTime(_t1); Pause(); }
        }
    }

    // ---- UI entry points ----
    public void Play()       { if (!_ready) return; _playing = true;  _playable.SetSpeed(speed); }
    public void Pause()      { if (!_ready) return; _playing = false; _playable.SetSpeed(0); }
    public void TogglePlay() { if (_playing) Pause(); else Play(); }
    public void Restart()    { if (!_ready) return; _playable.SetTime(_t0); Play(); }
    public void ToggleLoop() { loop = !loop; }
    public void SetSpeed(float s) { speed = Mathf.Clamp(s, 0.1f, 2f); if (_playing) _playable.SetSpeed(speed); }
    public void Scrub01(float f) { if (!_ready) return; _playable.SetTime(_t0 + Mathf.Clamp01(f) * (_t1 - _t0)); _graph.Evaluate(0f); }
    /// HeadYaw only: recall the station to wherever the learner is standing now.
    public void ReplaceHere() { if (placement == PlacementMode.HeadYaw) PlaceAtHead(); }

    void OnDestroy() { DestroyGraph(); }
}
