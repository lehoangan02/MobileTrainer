using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages step-by-step playback and UI for the "Deploy For Flight" tutorial on mobile.
/// </summary>
public class DeployTutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public string title;
        [TextArea(2, 4)]
        public string instruction;
        public AnimationClip clip;
        [Tooltip("Optional start trim timestamp in seconds within the clip.")]
        public float clipStart = 0f;
        [Tooltip("Optional end trim timestamp in seconds within the clip (0 = to end).")]
        public float clipEnd = 0f;
        [Tooltip("Transform/GameObject names in the rig to highlight during this step.")]
        public string[] highlightPartNames;
        [Tooltip("3D Camera Anchor in the scene. Drag and drop any GameObject here to position the camera directly in 3D space!")]
        public Transform cameraAnchor;
        [Tooltip("Custom camera pose for this step. If enabled, camera starts at this pose and resets to it.")]
        public StepCameraPose cameraPose;
    }

    [Header("Player Reference")]
    [SerializeField] private TutorialPlayer player;

    [Header("Ghost Skin / Material Highlighting")]
    [SerializeField] private TutorialGhostSkin ghostSkin;

    [Header("UI Elements (Optional/Assignable)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI stepCounterText;
    [SerializeField] private TextMeshProUGUI playPauseButtonText;
    [SerializeField] private Text playPauseButtonLegacyText;
    [SerializeField] private Slider timelineSlider;
    [SerializeField] private Selectable nextButton;
    [SerializeField] private Selectable prevButton;

    [Header("Playback Speed UI")]
    [SerializeField] private float[] speedSteps = new float[] { 0.25f, 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f };
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Text speedLegacyText;
    [SerializeField] private Selectable slowerButton;
    [SerializeField] private Selectable fasterButton;

    [Header("Scene Navigation")]
    [SerializeField] private string selectScreenSceneName = "TutorialSelectScreen";
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Camera Control")]
    [SerializeField] private ModelCameraController cameraController;

    [Header("Scene Default Camera Pose")]
    [Tooltip("Default camera starting pose for this entire scene. Fallback when a step does not have custom camera enabled.")]
    [SerializeField] private StepCameraPose sceneDefaultPose;
    [SerializeField] private float stepCameraTransitionDuration = 0.45f;

    [Header("Tutorial Sequence")]
    [SerializeField] private List<Step> steps = new();

    private int currentStepIndex = 0;
    private bool isScrubbing = false;
    private bool wasPlayingBeforeScrub = false;

    public int CurrentStepIndex => currentStepIndex;
    public int StepCount => steps.Count;
    public List<Step> Steps => steps;
    public StepCameraPose SceneDefaultPose { get => sceneDefaultPose; set => sceneDefaultPose = value; }
    public ModelCameraController CameraController => cameraController;

    private void Start()
    {
        if (player == null)
        {
            player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        }

        if (sceneLoader == null)
        {
            sceneLoader = UnityEngine.Object.FindFirstObjectByType<SceneLoader>();
        }

        if (cameraController == null)
        {
            cameraController = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
        }

        if (cameraController != null)
        {
            if (sceneDefaultPose.enabled)
            {
                cameraController.SceneDefaultPose = sceneDefaultPose;
            }
            else if (cameraController.SceneDefaultPose.enabled)
            {
                sceneDefaultPose = cameraController.SceneDefaultPose;
            }
        }

        if (ghostSkin == null)
        {
            ghostSkin = UnityEngine.Object.FindFirstObjectByType<TutorialGhostSkin>();
        }

        if (timelineSlider != null)
        {
            SetupSliderHitTarget(timelineSlider);

            timelineSlider.onValueChanged.RemoveListener(OnSliderScrub);
            timelineSlider.onValueChanged.AddListener(OnSliderScrub);

            var bridge = timelineSlider.GetComponent<SliderEventBridge>();
            if (bridge == null)
            {
                bridge = timelineSlider.gameObject.AddComponent<SliderEventBridge>();
            }
            bridge.targetSlider = timelineSlider;
            bridge.EnsureTouchHitArea();
            bridge.OnPointerDownEvent -= HandleSliderPointerDown;
            bridge.OnPointerDownEvent += HandleSliderPointerDown;
            bridge.OnPointerUpEvent -= HandleSliderPointerUp;
            bridge.OnPointerUpEvent += HandleSliderPointerUp;
        }

        if (steps.Count > 0)
        {
            GoToStep(0, false);
        }

        UpdateSpeedUI(true);
    }

    private void Update()
    {
        if (player == null) return;

        // Update scrubber if user is not actively dragging it
        if (timelineSlider != null && !isScrubbing)
        {
            timelineSlider.SetValueWithoutNotify(player.Progress01);
        }

        string playPauseStr = player.IsPlaying ? "PAUSE" : "PLAY";
        if (playPauseButtonText != null)
        {
            playPauseButtonText.text = playPauseStr;
        }
        if (playPauseButtonLegacyText != null)
        {
            playPauseButtonLegacyText.text = playPauseStr;
        }

        UpdateSpeedUI(false);
    }

    public void GoToStep(int index)
    {
        GoToStep(index, true);
    }

    public void GoToStep(int index, bool animateCamera)
    {
        if (steps == null || steps.Count == 0) return;

        isScrubbing = false;
        wasPlayingBeforeScrub = false;

        currentStepIndex = Mathf.Clamp(index, 0, steps.Count - 1);
        Step s = steps[currentStepIndex];

        if (timelineSlider != null)
        {
            timelineSlider.SetValueWithoutNotify(0f);
        }

        if (titleText != null) titleText.text = s.title;
        if (instructionText != null) instructionText.text = s.instruction;
        if (stepCounterText != null) stepCounterText.text = $"{currentStepIndex + 1} / {steps.Count}";

        if (prevButton != null) prevButton.interactable = currentStepIndex > 0;
        if (nextButton != null) nextButton.interactable = currentStepIndex < steps.Count - 1;

        if (player != null && s.clip != null)
        {
            player.PlayClip(s.clip, s.clipStart, s.clipEnd);
        }

        UpdateStepHighlight(s, currentStepIndex);
        ApplyStepCamera(s, animateCamera);
    }

    public void ApplyStepCamera(Step s, bool animate)
    {
        if (cameraController == null)
        {
            cameraController = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
            if (cameraController == null) return;
        }

        StepCameraPose pose;
        if (s != null && s.cameraAnchor != null)
        {
            Transform pivotTarget = cameraController.TargetPivotTransform;
            Camera anchorCam = s.cameraAnchor.GetComponent<Camera>();
            float fov = anchorCam != null ? anchorCam.fieldOfView : 55f;
            pose = StepCameraPose.CreateFromTransform(s.cameraAnchor, pivotTarget, fov);
        }
        else if (s != null && s.cameraPose.enabled)
        {
            pose = s.cameraPose;
        }
        else
        {
            pose = sceneDefaultPose;
        }

        cameraController.SetActivePose(pose, animate, stepCameraTransitionDuration);
    }

    public void NextStep()
    {
        if (currentStepIndex < steps.Count - 1)
        {
            GoToStep(currentStepIndex + 1);
        }
    }

    public void PreviousStep()
    {
        if (currentStepIndex > 0)
        {
            GoToStep(currentStepIndex - 1);
        }
    }

    public void TogglePlayPause()
    {
        if (player != null)
        {
            if (isScrubbing)
            {
                wasPlayingBeforeScrub = !wasPlayingBeforeScrub;
            }
            else
            {
                player.TogglePlay();
            }
        }
    }

    public void ReplayCurrentStep()
    {
        isScrubbing = false;
        wasPlayingBeforeScrub = false;
        if (timelineSlider != null)
        {
            timelineSlider.SetValueWithoutNotify(0f);
        }
        if (player != null)
        {
            player.Restart();
        }
    }

    private void HandleSliderPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        OnSliderPointerDown();
    }

    private void HandleSliderPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        OnSliderPointerUp();
    }

    public void OnSliderPointerDown()
    {
        isScrubbing = true;
        if (player != null)
        {
            wasPlayingBeforeScrub = player.IsPlaying;
            player.Pause();
        }
    }

    public void OnSliderPointerUp()
    {
        isScrubbing = false;
        if (player != null)
        {
            if (timelineSlider != null)
            {
                player.Scrub01(timelineSlider.value);
            }

            if (wasPlayingBeforeScrub)
            {
                player.Play();
            }
        }
    }

    public void OnSliderScrub(float value)
    {
        if (player != null)
        {
            player.Scrub01(value);
        }
    }

    private void SetupSliderHitTarget(Slider slider)
    {
        if (slider == null) return;
        if (slider.transform.Find("TouchHitArea") != null) return;

        GameObject hitGo = new GameObject("TouchHitArea", typeof(RectTransform));
        hitGo.transform.SetParent(slider.transform, false);
        hitGo.transform.SetAsFirstSibling();

        RectTransform rt = hitGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(40f, 70f);

        Image img = hitGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
    }

    public void BackToTutorialSelect()
    {
        if (sceneLoader == null)
        {
            sceneLoader = UnityEngine.Object.FindFirstObjectByType<SceneLoader>();
        }

        if (sceneLoader != null)
        {
            sceneLoader.LoadScene(selectScreenSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(selectScreenSceneName);
        }
    }

    public void ReturnToSelectScreen()
    {
        BackToTutorialSelect();
    }

    /// <summary>
    /// Resets the 3D model camera back to its initial original position, rotation, and distance.
    /// </summary>
    public void ResetCameraOrientation()
    {
        if (cameraController != null)
        {
            cameraController.ResetOrientation();
        }
        else
        {
            var cam = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
            if (cam != null)
            {
                cam.ResetOrientation();
            }
        }
    }

    // ---- Playback Speed Control ----

    public float CurrentPlaybackSpeed => player != null ? player.speed : 1f;

    private float lastReportedSpeed = -1f;

    public void DecreasePlaybackSpeed()
    {
        if (player == null) return;
        float cur = player.speed;
        float target = speedSteps[0];
        for (int i = speedSteps.Length - 1; i >= 0; i--)
        {
            if (speedSteps[i] < cur - 0.05f)
            {
                target = speedSteps[i];
                break;
            }
        }
        player.SetSpeed(target);
        UpdateSpeedUI(true);
    }

    public void IncreasePlaybackSpeed()
    {
        if (player == null) return;
        float cur = player.speed;
        float target = speedSteps[speedSteps.Length - 1];
        for (int i = 0; i < speedSteps.Length; i++)
        {
            if (speedSteps[i] > cur + 0.05f)
            {
                target = speedSteps[i];
                break;
            }
        }
        player.SetSpeed(target);
        UpdateSpeedUI(true);
    }

    public void ResetPlaybackSpeed()
    {
        if (player != null)
        {
            player.SetSpeed(1.0f);
        }
        UpdateSpeedUI(true);
    }

    public void SetPlaybackSpeed(float s)
    {
        if (player != null)
        {
            player.SetSpeed(s);
        }
        UpdateSpeedUI(true);
    }

    public void UpdateSpeedUI(bool force = false)
    {
        float spd = player != null ? player.speed : 1f;
        if (!force && Mathf.Approximately(spd, lastReportedSpeed)) return;
        lastReportedSpeed = spd;

        string spdStr = $"{spd:0.##}x";
        string fullStr = $"SPEED: {spdStr}";

        if (speedText != null)
        {
            speedText.text = fullStr;
        }
        if (speedLegacyText != null)
        {
            speedLegacyText.text = fullStr;
        }

        if (slowerButton != null)
        {
            slowerButton.interactable = spd > speedSteps[0] + 0.01f;
        }
        if (fasterButton != null)
        {
            fasterButton.interactable = spd < speedSteps[speedSteps.Length - 1] - 0.01f;
        }
    }

    // ---- Active Part Highlighting ----

    public void UpdateStepHighlight(Step s, int index)
    {
        if (ghostSkin == null)
        {
            ghostSkin = UnityEngine.Object.FindFirstObjectByType<TutorialGhostSkin>();
            if (ghostSkin == null) return;
        }

        string[] targets = (s != null && s.highlightPartNames != null && s.highlightPartNames.Length > 0)
            ? s.highlightPartNames
            : GetDefaultHighlightNames(index, s?.title, s?.clip);

        if (targets != null && targets.Length > 0)
        {
            ghostSkin.HighlightByNames(targets);
        }
        else
        {
            ghostSkin.ClearHighlights();
        }
    }

    public void RefreshCurrentHighlight()
    {
        if (steps != null && currentStepIndex >= 0 && currentStepIndex < steps.Count)
        {
            UpdateStepHighlight(steps[currentStepIndex], currentStepIndex);
        }
    }

    public static string[] GetDefaultHighlightNames(int index, string title, AnimationClip clip)
    {
        string t = title?.ToLowerInvariant() ?? "";
        string c = clip != null ? clip.name.ToLowerInvariant() : "";

        if (t.Contains("battery") || c.Contains("battery"))
            return new[] { "Ghost_Battery_Upper" };

        if (t.Contains("fan blade") || c.Contains("fan_blade"))
            return new[] { "Fan_1", "Fan_2", "Fan_3", "Fan_4" };

        if (t.Contains("one landing gear") || c.Contains("landing_gear_2"))
            return new[] { "Landing_Gear_Root_2" };

        if (t.Contains("other landing gear") || c.Contains("landing_gear_1"))
            return new[] { "Landing_Gear_Root_1" };

        if (t.Contains("front-left arm") || c.Contains("front_left_subarm"))
            return new[] { "Body_Front_Left" };

        if (t.Contains("front-right arm") || c.Contains("front_right_subarm"))
            return new[] { "Body_Front_Right" };

        if (t.Contains("back-left arm") || c.Contains("back_left_subarm"))
            return new[] { "Body_Back_Left" };

        if (t.Contains("back-right arm") || c.Contains("back_right_subarm"))
            return new[] { "Body_Back_Right" };

        if (t.Contains("both halves") || c.Contains("both_body_arms"))
            return new[] { "Body_Half_Left", "Body_Half_Right" };

        if (t.Contains("front-left latch") || c.Contains("front_left_latch"))
            return new[] { "Clamp_Front_Left_Latch" };

        if (t.Contains("front-left slider") || c.Contains("front_left_slider"))
            return new[] { "Clamp_Front_Left_Slider" };

        if (t.Contains("front-right latch") || c.Contains("front_right_latch"))
            return new[] { "Clamp_Front_Right_Latch" };

        if (t.Contains("front-right slider") || c.Contains("front_right_slider"))
            return new[] { "Clamp_Front_Right_Slider" };

        if (t.Contains("back-left latch") || c.Contains("back_left_latch"))
            return new[] { "Clamp_Back_Left_Latch" };

        if (t.Contains("back-left slider") || c.Contains("back_left_slider"))
            return new[] { "Clamp_Back_Left_Slider" };

        if (t.Contains("back-right latch") || c.Contains("back_right_latch"))
            return new[] { "Clamp_Back_Right_Latch" };

        if (t.Contains("back-right slider") || c.Contains("back_right_slider"))
            return new[] { "Clamp_Back_Right_Slider" };

        if (t.Contains("left body latch") || c.Contains("body_left_latch"))
            return new[] { "Clamp_Body_Left_Latch" };

        if (t.Contains("left body slider") || c.Contains("body_left_slider"))
            return new[] { "Clamp_Body_Left_Slider" };

        if (t.Contains("right body latch") || c.Contains("body_right_latch"))
            return new[] { "Clamp_Body_Right_Latch" };

        if (t.Contains("right body slider") || c.Contains("body_right_slider"))
            return new[] { "Clamp_Body_Right_Slider" };

        return System.Array.Empty<string>();
    }

#if UNITY_EDITOR
    [ContextMenu("Populate 27 Deploy Steps")]
    public void PopulateDefaultSteps()
    {
        string animFolder = "Assets/Screens/DeployForFlightScreen/Anim";

        (string title, string instruction, string clipName, string[] highlightParts)[] defs = new[]
        {
            ("Deploy one landing gear", "Use the THUMB to push the button on each leg of the landing gear AND grab the leg AT THE SAME TIME to swing it outwards.", "unfold_open_landing_gear_2.anim", new[] { "Landing_Gear_Root_2" }),
            ("Deploy the other landing gear", "Use the THUMB to push the button on each leg of the landing gear AND grab the leg AT THE SAME TIME to swing it outwards.", "unfold_open_landing_gear_1.anim", new[] { "Landing_Gear_Root_1" }),
            ("Swing out the front-left arm", "Use the THUMB to push the front-right release button AND grab the sub-arm AT THE SAME TIME to swing it outwards.", "unfold_open_front_left_subarm.anim", new[] { "Body_Front_Left" }),
            ("Open the front-left latch", "Pinch the front-left latch and swing it fully open.", "unfold_open_front_left_latch.anim", new[] { "Clamp_Front_Left_Latch" }),
            ("Return the front-left slider", "Slide the front-left slider all the way home.", "unfold_slide_front_left_slider.anim", new[] { "Clamp_Front_Left_Slider" }),
            ("Close the front-left latch", "Swing the front-left latch shut to lock the slider.", "unfold_close_front_left_latch.anim", new[] { "Clamp_Front_Left_Latch" }),
            ("Swing out the front-right arm", "Use the THUMB to push the front-right release button AND grab the sub-arm AT THE SAME TIME to swing it outwards.", "unfold_open_front_right_subarm.anim", new[] { "Body_Front_Right" }),
            ("Open the front-right latch", "Grab the front-right latch and swing it fully open.", "unfold_open_front_right_latch.anim", new[] { "Clamp_Front_Right_Latch" }),
            ("Return the front-right slider", "Slide the front-right slider all the way home.", "unfold_slide_front_right_slider.anim", new[] { "Clamp_Front_Right_Slider" }),
            ("Close the front-right latch", "Swing the front-right latch shut to lock the slider.", "unfold_close_front_right_latch.anim", new[] { "Clamp_Front_Right_Latch" }),
            ("Swing out the back-left arm", "Use the THUMB to push the back-left release button AND grab the sub-arm AT THE SAME TIME to swing it outwards.", "unfold_open_back_left_subarm.anim", new[] { "Body_Back_Left" }),
            ("Open the back-left latch", "Grab the back-left latch and swing it fully open.", "unfold_open_back_left_latch.anim", new[] { "Clamp_Back_Left_Latch" }),
            ("Return the back-left slider", "Slide the back-left slider all the way home.", "unfold_slide_back_left_slider.anim", new[] { "Clamp_Back_Left_Slider" }),
            ("Close the back-left latch", "Swing the back-left latch shut to lock the slider.", "unfold_close_back_left_latch.anim", new[] { "Clamp_Back_Left_Latch" }),
            ("Swing out the back-right arm", "Use the THUMB to push the back-right release button AND grab the sub-arm AT THE SAME TIME to swing it outwards.", "unfold_open_back_right_subarm.anim", new[] { "Body_Back_Right" }),
            ("Open the back-right latch", "Pinch the back-right latch and swing it fully open.", "unfold_open_back_right_latch.anim", new[] { "Clamp_Back_Right_Latch" }),
            ("Return the back-right slider", "Slide the back-right slider all the way home.", "unfold_slide_back_right_slider.anim", new[] { "Clamp_Back_Right_Slider" }),
            ("Close the back-right latch", "Swing the back-right latch shut to lock the slider.", "unfold_close_back_right_latch.anim", new[] { "Clamp_Back_Right_Latch" }),
            ("Open both halves", "Grab both halves of the drone and rotate them back out to zero together.", "unfold_open_both_body_arms.anim", new[] { "Body_Half_Left", "Body_Half_Right" }),
            ("Open the left body latch", "Grab the left body latch and swing it fully open.", "unfold_open_body_left_latch.anim", new[] { "Clamp_Body_Left_Latch" }),
            ("Return the left body slider", "Slide the left body slider all the way home.", "unfold_slide_body_left_slider.anim", new[] { "Clamp_Body_Left_Slider" }),
            ("Close the left body latch", "Swing the left body latch shut to lock the slider.", "unfold_close_body_left_latch.anim", new[] { "Clamp_Body_Left_Latch" }),
            ("Open the right body latch", "Grab the right body latch and swing it fully open.", "unfold_open_body_right_latch.anim", new[] { "Clamp_Body_Right_Latch" }),
            ("Return the right body slider", "Slide the right body slider all the way home.", "unfold_slide_body_right_slider.anim", new[] { "Clamp_Body_Right_Slider" }),
            ("Close the right body latch", "Swing the right body latch shut to lock the slider.", "unfold_close_body_right_latch.anim", new[] { "Clamp_Body_Right_Latch" }),
            ("Return the fan blades", "Rotate every fan blade back out of the folding range.", "unfold_extend_all_4_pairs_fan_blade.anim", new[] { "Fan_1", "Fan_2", "Fan_3", "Fan_4" }),
            ("Dock the battery", "Carry the battery unit over the battery box until it snaps home.", "unfold_attatch_battery.anim", new[] { "Ghost_Battery_Upper" })
        };

        Undo.RecordObject(this, "Populate Default Deploy Steps");
        steps.Clear();

        foreach (var def in defs)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animFolder}/{def.clipName}");
            steps.Add(new Step
            {
                title = def.title,
                instruction = def.instruction,
                clip = clip,
                clipStart = 0f,
                clipEnd = 0f,
                highlightPartNames = def.highlightParts
            });
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[DeployTutorialManager] Populated {steps.Count} default deploy steps.");
    }

    [ContextMenu("Capture Scene View to Current Step Camera")]
    public void CaptureSceneViewToCurrentStep()
    {
        if (steps == null || currentStepIndex < 0 || currentStepIndex >= steps.Count) return;
        Undo.RecordObject(this, "Capture Step Camera Pose");
        Transform targetPivot = cameraController != null ? cameraController.TargetPivotTransform : null;
        steps[currentStepIndex].cameraPose = StepCameraPose.CreateFromSceneView(targetPivot);
        steps[currentStepIndex].cameraPose.enabled = true;
        EditorUtility.SetDirty(this);
        Debug.Log($"[DeployTutorialManager] Captured Step {currentStepIndex + 1} ('{steps[currentStepIndex].title}') camera from SceneView: {steps[currentStepIndex].cameraPose.SummaryText}");
    }

    [ContextMenu("Capture Scene View to Scene Default")]
    public void CaptureSceneViewToSceneDefault()
    {
        Undo.RecordObject(this, "Capture Scene Default Camera");
        Transform targetPivot = cameraController != null ? cameraController.TargetPivotTransform : null;
        sceneDefaultPose = StepCameraPose.CreateFromSceneView(targetPivot);
        sceneDefaultPose.enabled = true;
        if (cameraController != null)
        {
            cameraController.SceneDefaultPose = sceneDefaultPose;
            EditorUtility.SetDirty(cameraController);
        }
        EditorUtility.SetDirty(this);
        Debug.Log($"[DeployTutorialManager] Captured Scene Default Camera from SceneView: {sceneDefaultPose.SummaryText}");
    }

    [ContextMenu("Preview Current Step Camera")]
    public void PreviewCurrentStepCamera()
    {
        if (steps == null || currentStepIndex < 0 || currentStepIndex >= steps.Count) return;
        var step = steps[currentStepIndex];
        if (step.cameraPose.enabled)
        {
            step.cameraPose.ApplyPreviewInEditor();
        }
        else if (sceneDefaultPose.enabled)
        {
            sceneDefaultPose.ApplyPreviewInEditor();
        }
    }
#endif
}
