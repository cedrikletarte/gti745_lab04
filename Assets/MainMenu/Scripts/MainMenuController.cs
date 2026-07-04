using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the game mode selection buttons in the MainMenu scene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] string soloSceneName = "Solo";
    [SerializeField] string rythmeSceneName = "Rythme";

    public void SelectSoloMode()
    {
        Debug.Log("Mode Solo sélectionné.");
        SceneManager.LoadScene(soloSceneName);
    }

    public void SelectRythmeMode()
    {
        Debug.Log("Mode Rythme sélectionné.");
        // TODO: charger la scène du mode Rythme lorsqu'elle sera disponible.
    }
}
