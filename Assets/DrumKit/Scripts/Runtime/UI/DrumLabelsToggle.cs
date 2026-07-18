using UnityEngine;

public class DrumLabelsToggle : MonoBehaviour
{
    public GameObject drumLabels;

    public void ToggleLabels()
    {
        drumLabels.SetActive(!drumLabels.activeSelf);
    }
}