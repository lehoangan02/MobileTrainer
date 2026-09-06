using UnityEngine;
using UnityEngine.SceneManagement;
using Lean.Gui;

/// <summary>
/// Handles navigation from the Tutorial Select Screen to specific tutorial scenes.
/// </summary>
public class TutorialSelectController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Tutorial Buttons")]
    [SerializeField] private LeanButton foldForStorageButton;
    [SerializeField] private LeanButton deployForFlightButton;

    private void Awake()
    {
        ResolveDependencies();
        AutoHookButtons();
    }

    private void Start()
    {
        ResolveDependencies();
        AutoHookButtons();
    }

    private void ResolveDependencies()
    {
        if (sceneLoader == null)
        {
            sceneLoader = GetComponentInChildren<SceneLoader>(true);
            if (sceneLoader == null)
            {
                sceneLoader = Object.FindFirstObjectByType<SceneLoader>();
            }
        }
    }

    private void AutoHookButtons()
    {
        if (foldForStorageButton == null || deployForFlightButton == null)
        {
            var leanButtons = GetComponentsInChildren<LeanButton>(true);
            foreach (var btn in leanButtons)
            {
                string btnInfo = btn.gameObject.name;
                var uiText = btn.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (uiText != null) btnInfo += " " + uiText.text;
                var tmpText = btn.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmpText != null) btnInfo += " " + tmpText.text;

                if (foldForStorageButton == null && btnInfo.IndexOf("fold", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foldForStorageButton = btn;
                }
                else if (deployForFlightButton == null && btnInfo.IndexOf("deploy", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    deployForFlightButton = btn;
                }
            }
        }

        if (foldForStorageButton != null)
        {
            foldForStorageButton.OnClick.RemoveListener(OpenFoldForStorage);
            foldForStorageButton.OnClick.AddListener(OpenFoldForStorage);
            Debug.Log($"[TutorialSelectController] Fold For Storage button registered on '{foldForStorageButton.gameObject.name}'.");
        }

        if (deployForFlightButton != null)
        {
            deployForFlightButton.OnClick.RemoveListener(OpenDeployForFlight);
            deployForFlightButton.OnClick.AddListener(OpenDeployForFlight);
        }
    }

    public void OpenFoldForStorage()
    {
        Debug.Log("[TutorialSelectController] OpenFoldForStorage invoked.");
        Load("FoldForStorage");
    }

    public void OpenDeployForFlight()
    {
        Debug.Log("[TutorialSelectController] OpenDeployForFlight invoked.");
        if (Application.CanStreamedLevelBeLoaded("DeployForFlightScreen"))
        {
            Load("DeployForFlightScreen");
        }
        else
        {
            Debug.LogWarning("[TutorialSelectController] DeployForFlightScreen scene is not yet added to Build Settings or not implemented.");
        }
    }

    public void BackToStartScreen()
    {
        Debug.Log("[TutorialSelectController] BackToStartScreen invoked.");
        Load("StartScreen");
    }

    private void Load(string sceneName)
    {
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene(sceneName);
        }
        else
        {
            SceneLoader found = Object.FindFirstObjectByType<SceneLoader>();
            if (found != null)
            {
                found.LoadScene(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
