using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Colour set for a LeanGUI button built from the shared "Tap Button" prefab, so a disabled
/// button can be greyed without fighting the prefab's own colour transitions.
/// </summary>
public class ButtonTheme : MonoBehaviour
{
    [SerializeField] private Image cap;
    [SerializeField] private Color normal = new Color(0.957f, 0.922f, 0.322f);
    [SerializeField] private Color disabled = new Color(0.45f, 0.46f, 0.50f);

    public static void SetInteractable(LeanButton button, bool on)
    {
        button.interactable = on;
        var theme = button.GetComponent<ButtonTheme>();
        if (theme != null && theme.cap != null) theme.cap.color = on ? theme.normal : theme.disabled;
    }
}
