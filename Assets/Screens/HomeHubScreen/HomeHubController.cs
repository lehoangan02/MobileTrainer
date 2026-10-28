using UnityEngine;
using UnityEngine.SceneManagement;
using Lean.Gui;

/// <summary>
/// Top-level module launcher: Drone Tutorials and Room Scans.
/// </summary>
public class HomeHubController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Module Cards")]
    [SerializeField] private LeanButton droneTutorialsButton;
    [SerializeField] private LeanButton roomScansButton;

    private void Awake()
    {
        ResolveDependencies();
        HookButtons();
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

    private void HookButtons()
    {
        if (droneTutorialsButton != null)
        {
            droneTutorialsButton.OnClick.RemoveListener(OpenDroneTutorials);
            droneTutorialsButton.OnClick.AddListener(OpenDroneTutorials);
        }

        if (roomScansButton != null)
        {
            roomScansButton.OnClick.RemoveListener(OpenRoomScans);
            roomScansButton.OnClick.AddListener(OpenRoomScans);
        }
    }

    public void OpenDroneTutorials()
    {
        Load("TutorialSelectScreen");
    }

    public void OpenRoomScans()
    {
        Load("RoomScanLibrary");
    }

    private void Load(string sceneName)
    {
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
