using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles navigation from the Tutorial Select Screen to specific tutorial scenes.
/// </summary>
public class TutorialSelectController : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private SceneLoader sceneLoader;

    public void OpenFoldForStorage()
    {
        Load("FoldForStorage");
    }

    public void OpenDeployForFlight()
    {
        Load("DeployForFlightScreen");
    }

    public void BackToStartScreen()
    {
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
            SceneLoader found = UnityEngine.Object.FindFirstObjectByType<SceneLoader>();
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
