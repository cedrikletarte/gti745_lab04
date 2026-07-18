using UnityEngine;

public class HelpPanelToggle : MonoBehaviour
{
    public GameObject helpPanel;
    public GameObject openHelpButton;

    void Start()
    {
        helpPanel.SetActive(true);
        openHelpButton.SetActive(false);
    }

    public void CloseHelp()
    {
        helpPanel.SetActive(false);
        openHelpButton.SetActive(true);
    }

    public void OpenHelp()
    {
        helpPanel.SetActive(true);
        openHelpButton.SetActive(false);
    }
}