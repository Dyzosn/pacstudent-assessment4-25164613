using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    // Load Level 1 scene
    public void LoadLevel1()
    {
        SceneManager.LoadScene("Level01");
    }

    // Load Level 2 scene (placeholder for now)
    public void LoadLevel2()
    {
        // Log message since Level 2 doesn't exist yet
        Debug.Log("Level 2 not implemented yet - will load InnovationScene in Phase 100%");

        // Uncomment when InnovationScene is created:
        // SceneManager.LoadScene("InnovationScene");
    }

    // Reload current scene
    public void ReloadCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    // Quit application
    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}