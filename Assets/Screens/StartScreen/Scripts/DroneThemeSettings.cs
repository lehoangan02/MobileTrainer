using System;
using UnityEngine;
using UnityEngine.UI;
using Lean.Gui;

/// <summary>
/// Controls the radio buttons for selecting drone themes/materials inside the Start Screen Settings Modal.
/// Persists the selected theme index to PlayerPrefs ("SelectedDroneTheme") so it is automatically applied
/// when the FoldForStorage tutorial scene is loaded.
/// </summary>
public class DroneThemeSettings : MonoBehaviour
{
    public const string PrefKey = "SelectedDroneTheme";

    [Serializable]
    public class ThemeRadioOption
    {
        public string themeName;
        public LeanToggle toggle;
        public LeanButton button;
        public Image innerDot;
        public Image outerRing;
        public Text label;
    }

    [Header("3 Material Theme Options")]
    [Tooltip("0 = Version 1 (Cyan Ghost), 1 = Version 2 (Carbon + Yellow), 2 = Version 3 (Carbon + Cyan)")]
    public ThemeRadioOption[] options = new ThemeRadioOption[3];

    [Header("Colors")]
    public Color activeDotColor = new Color(0.12f, 0.58f, 0.95f, 1f);       // Bright blue/cyan indicator
    public Color inactiveDotColor = new Color(0f, 0f, 0f, 0f);                // Transparent
    public Color activeRingColor = new Color(0.12f, 0.58f, 0.95f, 1f);      // Active border
    public Color inactiveRingColor = new Color(0.70f, 0.74f, 0.78f, 1f);    // Neutral border
    public Color activeTextColor = new Color(0.10f, 0.15f, 0.22f, 1f);      // Bold dark text
    public Color inactiveTextColor = new Color(0.40f, 0.45f, 0.52f, 1f);    // Soft dark text

    private bool isUpdatingVisuals = false;

    private void Awake()
    {
        if (!PlayerPrefs.HasKey(PrefKey))
        {
            PlayerPrefs.SetInt(PrefKey, 0);
            PlayerPrefs.Save();
        }

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            if (options[i] != null && options[i].button != null)
            {
                options[i].button.OnClick.AddListener(() => SelectTheme(index));
            }
        }
    }

    private void Start()
    {
        int savedTheme = PlayerPrefs.GetInt(PrefKey, 0);
        savedTheme = Mathf.Clamp(savedTheme, 0, 2);
        ApplySelectionVisuals(savedTheme);
    }

    private void OnEnable()
    {
        int savedTheme = PlayerPrefs.GetInt(PrefKey, 0);
        savedTheme = Mathf.Clamp(savedTheme, 0, 2);
        ApplySelectionVisuals(savedTheme);
    }

    /// <summary>
    /// Selects the theme by index (0, 1, or 2), saves to PlayerPrefs, and updates visuals.
    /// </summary>
    public void SelectTheme(int index)
    {
        if (isUpdatingVisuals) return;

        index = Mathf.Clamp(index, 0, 2);
        PlayerPrefs.SetInt(PrefKey, index);
        PlayerPrefs.Save();
        Debug.Log($"[DroneThemeSettings] Selected theme: Version {index + 1} ({options[index]?.themeName})");

        ApplySelectionVisuals(index);
    }

    /// <summary>
    /// Updates toggles, radio dots, and labels to reflect current selection.
    /// </summary>
    public void ApplySelectionVisuals(int activeIndex)
    {
        isUpdatingVisuals = true;
        try
        {
        for (int i = 0; i < options.Length; i++)
        {
            var opt = options[i];
            if (opt == null) continue;

            bool isSelected = (i == activeIndex);

            if (opt.toggle != null && opt.toggle.On != isSelected)
            {
                opt.toggle.Set(isSelected);
            }

            if (opt.innerDot != null)
            {
                opt.innerDot.color = isSelected ? activeDotColor : inactiveDotColor;
            }

            if (opt.outerRing != null)
            {
                opt.outerRing.color = isSelected ? activeRingColor : inactiveRingColor;
            }

            if (opt.label != null)
            {
                opt.label.color = isSelected ? activeTextColor : inactiveTextColor;
            }
        }
    }
    finally
    {
        isUpdatingVisuals = false;
    }
}
}
