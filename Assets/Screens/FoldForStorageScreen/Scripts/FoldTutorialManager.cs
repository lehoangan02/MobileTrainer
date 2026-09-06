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
    }

    [Header("Player Reference")]
    [SerializeField] private TutorialPlayer player;

    [Header("UI Elements (Optional/Assignable)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI stepCounterText;
    [SerializeField] private TextMeshProUGUI playPauseButtonText;
    [SerializeField] private Text playPauseButtonLegacyText;
    [SerializeField] private Slider timelineSlider;
    [SerializeField] private Selectable nextButton;
    [SerializeField] private Selectable prevButton;

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

        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.AddListener(OnSliderScrub);
        }

        if (steps.Count > 0)
        {
            GoToStep(0);
        }
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

#if UNITY_EDITOR
    [ContextMenu("Populate 26 Fold Steps")]
    public void PopulateDefaultSteps()
    {
        string animFolder = "Assets/Screens/FoldForStorageScreen/Anim";

        (string title, string instruction, string clipName)[] defs = new[]
        {
            ("Release the battery", "Grab the battery handle and rotate it until the latch releases.", "Release_Battery.anim"),
            ("Lift the battery off", "Keep hold of the handle and lift the upper battery unit clear of the drone.", "Release_Battery.anim"),
            ("Set all fan blades", "Rotate each of the eight fan blades into the folding range.", "set_fan_blades.anim"),
            ("Open the right body latch", "Grab the right body latch and swing it fully open.", "open_body_right_latch.anim"),
            ("Slide the right body slider", "Slide the right body slider back past its stop.", "slide_body_right_slider.anim"),
            ("Close the right body latch", "Swing the right body latch shut to lock the slider.", "close_body_right_latch.anim"),
            ("Open the left body latch", "Grab the left body latch and swing it fully open.", "open_body_left_latch.anim"),
            ("Slide the left body slider", "Slide the left body slider back past its stop.", "slide_body_left_slider.anim"),
            ("Close the left body latch", "Swing the left body latch shut to lock the slider.", "close_body_left_latch.anim"),
            ("Fold both halves", "Grab both halves of the drone and rotate them inward together past 85 degrees.", "fold_both_body_arms.anim"),
            ("Open the front-right latch", "Grab the front-right latch and swing it fully open.", "open_front_right_latch.anim"),
            ("Slide the front-right slider", "Slide the front-right slider back past its stop.", "slide_front_right_slider.anim"),
            ("Close the front-right latch", "Swing the front-right latch shut to lock the slider.", "close_front_right_latch.anim"),
            ("Open the front-left latch", "Grab the front-left latch and swing it fully open.", "open_front_left_latch.anim"),
            ("Slide the front-left slider", "Slide the front-left slider back past its stop.", "slide_front_left_slider.anim"),
            ("Close the front-left latch", "Swing the front-left latch shut to lock the slider.", "close_front_left_latch.anim"),
            ("Fold the front arms", "Grab both front sub-arms and rotate them together past 90 degrees.", "fold_both_front_subarms.anim"),
            ("Open the back-right latch", "Grab the back-right latch and swing it fully open.", "open_back_right_latch.anim"),
            ("Slide the back-right slider", "Slide the back-right slider back past its stop.", "slide_back_right_slider.anim"),
            ("Close the back-right latch", "Swing the back-right latch shut to lock the slider.", "close_back_right_latch.anim"),
            ("Open the back-left latch", "Grab the back-left latch and swing it fully open.", "open_back_left_latch.anim"),
            ("Slide the back-left slider", "Slide the back-left slider back past its stop.", "slide_back_left_slider.anim"),
            ("Close the back-left latch", "Swing the back-left latch shut to lock the slider.", "close_back_left_latch.anim"),
            ("Fold the back arms", "Grab both rear sub-arms and rotate them together past 90 degrees.", "fold_both_back_subarms.anim"),
            ("Fold one landing gear", "Poke the button on each leg of either landing gear, then fold both of its legs past 85 degrees.", "fold_one_pair_landing_gear.anim"),
            ("Fold the other landing gear", "Do the same on the remaining landing gear: poke each leg button, then fold both legs.", "fold_another_pair_landing_gear.anim")
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
                clip = clip
            });
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[FoldTutorialManager] Populated {steps.Count} default fold steps.");
    }
#endif
}
