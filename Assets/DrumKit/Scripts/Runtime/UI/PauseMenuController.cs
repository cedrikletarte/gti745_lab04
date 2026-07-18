using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseBackground;
    public GameObject helpPanel;
    
    public void ShowHelp()
    {
        pauseBackground.SetActive(false);
        helpPanel.SetActive(true);
    }

    public void ShowPauseMenu()
    {
        pauseBackground.SetActive(true);
        helpPanel.SetActive(false);
    }

    public void QuitGame()
    {
        // load la scene du menu principal
        SceneManager.LoadScene("MainMenu");
    }
}
