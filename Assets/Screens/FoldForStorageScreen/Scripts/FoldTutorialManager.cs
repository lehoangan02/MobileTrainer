using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages step-by-step playback and UI for the "Fold For Storage" tutorial on mobile.
/// </summary>
public class FoldTutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public string title;
        [TextArea(2, 4)]
        public string instruction;
        public AnimationClip clip;
        [Tooltip("Transform/GameObject names in the rig to highlight with M_TutorialGhost_Red during this step.")]
        public string[] highlightPartNames;
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

    [Header("Tutorial Sequence (26 Steps)")]
    [SerializeField] private List<Step> steps = new();

    private int currentStepIndex = 0;
    private bool isScrubbing = false;

    public int CurrentStepIndex => currentStepIndex;
    public int StepCount => steps.Count;

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

        if (ghostSkin == null)
        {
            ghostSkin = UnityEngine.Object.FindFirstObjectByType<TutorialGhostSkin>();
        }

        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.AddListener(OnSliderScrub);
        }

        if (steps.Count > 0)
        {
            GoToStep(0);
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
        if (steps == null || steps.Count == 0) return;

        currentStepIndex = Mathf.Clamp(index, 0, steps.Count - 1);
        Step s = steps[currentStepIndex];

        if (titleText != null) titleText.text = s.title;
        if (instructionText != null) instructionText.text = s.instruction;
        if (stepCounterText != null) stepCounterText.text = $"{currentStepIndex + 1} / {steps.Count}";

        if (prevButton != null) prevButton.interactable = currentStepIndex > 0;
        if (nextButton != null) nextButton.interactable = currentStepIndex < steps.Count - 1;

        if (player != null && s.clip != null)
        {
            player.PlayClip(s.clip);
        }

        UpdateStepHighlight(s, currentStepIndex);
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
            player.TogglePlay();
        }
    }

    public void ReplayCurrentStep()
    {
        if (player != null)
        {
            player.Restart();
        }
    }

    public void OnSliderPointerDown()
    {
        isScrubbing = true;
    }

    public void OnSliderPointerUp()
    {
        isScrubbing = false;
    }

    public void OnSliderScrub(float value)
    {
        if (player != null)
        {
            player.Scrub01(value);
        }
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

    // ---- Active Part Red Highlighting (M_TutorialGhost_Red) ----

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

    /// <summary>
    /// Returns the active mechanical component names on the drone that change during each step.
    /// </summary>
    public static string[] GetDefaultHighlightNames(int index, string title, AnimationClip clip)
    {
        string t = title?.ToLowerInvariant() ?? "";
        string c = clip != null ? clip.name.ToLowerInvariant() : "";

        if (t.Contains("battery") || c.Contains("battery"))
            return new[] { "Ghost_Battery_Upper" };

        if (t.Contains("fan blade") || c.Contains("fan_blades"))
            return new[] { "Fan_1", "Fan_2", "Fan_3", "Fan_4" };

        if (t.Contains("right body latch") || c.Contains("body_right_latch"))
            return new[] { "Clamp_Body_Right_Latch" };

        if (t.Contains("right body slider") || c.Contains("body_right_slider"))
            return new[] { "Clamp_Body_Right_Slider" };

        if (t.Contains("left body latch") || c.Contains("body_left_latch"))
            return new[] { "Clamp_Body_Left_Latch" };

        if (t.Contains("left body slider") || c.Contains("body_left_slider"))
            return new[] { "Clamp_Body_Left_Slider" };

        if (t.Contains("both halves") || c.Contains("both_body_arms"))
            return new[] { "Body_Half_Left", "Body_Half_Right" };

        if (t.Contains("front-right latch") || c.Contains("front_right_latch"))
            return new[] { "Clamp_Front_Right_Latch" };

        if (t.Contains("front-right slider") || c.Contains("front_right_slider"))
            return new[] { "Clamp_Front_Right_Slider" };

        if (t.Contains("front-left latch") || c.Contains("front_left_latch"))
            return new[] { "Clamp_Front_Left_Latch" };

        if (t.Contains("front-left slider") || c.Contains("front_left_slider"))
            return new[] { "Clamp_Front_Left_Slider" };

        if (t.Contains("front arms") || c.Contains("front_subarms"))
            return new[] { "Body_Front_Left", "Body_Front_Right" };

        if (t.Contains("back-right latch") || c.Contains("back_right_latch"))
            return new[] { "Clamp_Back_Right_Latch" };

        if (t.Contains("back-right slider") || c.Contains("back_right_slider"))
            return new[] { "Clamp_Back_Right_Slider" };

        if (t.Contains("back-left latch") || c.Contains("back_left_latch"))
            return new[] { "Clamp_Back_Left_Latch" };

        if (t.Contains("back-left slider") || c.Contains("back_left_slider"))
            return new[] { "Clamp_Back_Left_Slider" };

        if (t.Contains("back arms") || c.Contains("back_subarms"))
            return new[] { "Body_Back_Left", "Body_Back_Right" };

        if (t.Contains("one landing gear") || c.Contains("one_pair_landing_gear"))
            return new[] { "Landing_Gear_Root_2" };

        if (t.Contains("other landing gear") || c.Contains("another_pair_landing_gear"))
            return new[] { "Landing_Gear_Root_1" };

        return System.Array.Empty<string>();
    }

#if UNITY_EDITOR
    [ContextMenu("Populate 26 Fold Steps")]
    public void PopulateDefaultSteps()
    {
        string animFolder = "Assets/Screens/FoldForStorageScreen/Anim";

        (string title, string instruction, string clipName, string[] highlightParts)[] defs = new[]
        {
            ("Release the battery", "Grab the battery handle and rotate it until the latch releases.", "Release_Battery.anim", new[] { "Ghost_Battery_Upper" }),
            ("Lift the battery off", "Keep hold of the handle and lift the upper battery unit clear of the drone.", "Release_Battery.anim", new[] { "Ghost_Battery_Upper" }),
            ("Set all fan blades", "Rotate each of the eight fan blades into the folding range.", "set_fan_blades.anim", new[] { "Fan_1", "Fan_2", "Fan_3", "Fan_4" }),
            ("Open the right body latch", "Grab the right body latch and swing it fully open.", "open_body_right_latch.anim", new[] { "Clamp_Body_Right_Latch" }),
            ("Slide the right body slider", "Slide the right body slider back past its stop.", "slide_body_right_slider.anim", new[] { "Clamp_Body_Right_Slider" }),
            ("Close the right body latch", "Swing the right body latch shut to lock the slider.", "close_body_right_latch.anim", new[] { "Clamp_Body_Right_Latch" }),
            ("Open the left body latch", "Grab the left body latch and swing it fully open.", "open_body_left_latch.anim", new[] { "Clamp_Body_Left_Latch" }),
            ("Slide the left body slider", "Slide the left body slider back past its stop.", "slide_body_left_slider.anim", new[] { "Clamp_Body_Left_Slider" }),
            ("Close the left body latch", "Swing the left body latch shut to lock the slider.", "close_body_left_latch.anim", new[] { "Clamp_Body_Left_Latch" }),
            ("Fold both halves", "Grab both halves of the drone and rotate them inward together past 85 degrees.", "fold_both_body_arms.anim", new[] { "Body_Half_Left", "Body_Half_Right" }),
            ("Open the front-right latch", "Grab the front-right latch and swing it fully open.", "open_front_right_latch.anim", new[] { "Clamp_Front_Right_Latch" }),
            ("Slide the front-right slider", "Slide the front-right slider back past its stop.", "slide_front_right_slider.anim", new[] { "Clamp_Front_Right_Slider" }),
            ("Close the front-right latch", "Swing the front-right latch shut to lock the slider.", "close_front_right_latch.anim", new[] { "Clamp_Front_Right_Latch" }),
            ("Open the front-left latch", "Grab the front-left latch and swing it fully open.", "open_front_left_latch.anim", new[] { "Clamp_Front_Left_Latch" }),
            ("Slide the front-left slider", "Slide the front-left slider back past its stop.", "slide_front_left_slider.anim", new[] { "Clamp_Front_Left_Slider" }),
            ("Close the front-left latch", "Swing the front-left latch shut to lock the slider.", "close_front_left_latch.anim", new[] { "Clamp_Front_Left_Latch" }),
            ("Fold the front arms", "Grab both front sub-arms and rotate them together past 90 degrees.", "fold_both_front_subarms.anim", new[] { "Body_Front_Left", "Body_Front_Right" }),
            ("Open the back-right latch", "Grab the back-right latch and swing it fully open.", "open_back_right_latch.anim", new[] { "Clamp_Back_Right_Latch" }),
            ("Slide the back-right slider", "Slide the back-right slider back past its stop.", "slide_back_right_slider.anim", new[] { "Clamp_Back_Right_Slider" }),
            ("Close the back-right latch", "Swing the back-right latch shut to lock the slider.", "close_back_right_latch.anim", new[] { "Clamp_Back_Right_Latch" }),
            ("Open the back-left latch", "Grab the back-left latch and swing it fully open.", "open_back_left_latch.anim", new[] { "Clamp_Back_Left_Latch" }),
            ("Slide the back-left slider", "Slide the back-left slider back past its stop.", "slide_back_left_slider.anim", new[] { "Clamp_Back_Left_Slider" }),
            ("Close the back-left latch", "Swing the back-left latch shut to lock the slider.", "close_back_left_latch.anim", new[] { "Clamp_Back_Left_Latch" }),
            ("Fold the back arms", "Grab both rear sub-arms and rotate them together past 90 degrees.", "fold_both_back_subarms.anim", new[] { "Body_Back_Left", "Body_Back_Right" }),
            ("Fold one landing gear", "Poke the button on each leg of either landing gear, then fold both of its legs past 85 degrees.", "fold_one_pair_landing_gear.anim", new[] { "Landing_Gear_Root_2" }),
            ("Fold the other landing gear", "Do the same on the remaining landing gear: poke each leg button, then fold both legs.", "fold_another_pair_landing_gear.anim", new[] { "Landing_Gear_Root_1" })
        };

        Undo.RecordObject(this, "Populate Default Fold Steps");
        steps.Clear();

        foreach (var def in defs)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animFolder}/{def.clipName}");
            steps.Add(new Step
            {
                title = def.title,
                instruction = def.instruction,
                clip = clip,
                highlightPartNames = def.highlightParts
            });
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[FoldTutorialManager] Populated {steps.Count} default fold steps with active part highlight targets.");
    }
#endif
}
