using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DrumKit.Rhythm;

/// <summary>
/// Toggles the game between play and pause from a controller button.
///
/// While paused: the game clock is frozen (Time.timeScale = 0), the in-game ModeLabel is
/// hidden and the PauseBackground overlay is shown. Resuming does the inverse. In rhythm
/// scenes an optional Conductor is also paused, because the music runs on the audio DSP
/// clock which Time.timeScale can't stop on its own.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("Pause overlay (dimmed background + buttons). Shown while paused, hidden while playing.")]
    public GameObject pauseBackground;

    [Tooltip("Help/instructions panel reachable from the pause menu.")]
    public GameObject helpPanel;

    [Tooltip("The in-game MODE label. Hidden while paused, shown while playing.")]
    public GameObject modeLabel;

    [Header("Input")]
    [Tooltip("Controller button that toggles pause/resume. Its 'performed' callback flips the pause state.")]
    public InputActionProperty pauseAction;

    [Header("Game")]
    [Tooltip("Optional. Assign in rhythm scenes so the music freezes too (audio runs on the DSP clock, which Time.timeScale can't stop). Leave empty in Solo/free-play.")]
    public Conductor conductor;

    [Header("Controllers (play <-> menu swap)")]
    [Tooltip("Objects enabled while paused and disabled while playing - e.g. the controllers' Near-Far (UI ray) interactors so the player can point at the pause menu, exactly like in the main menu.")]
    public GameObject[] showWhilePaused;

    [Tooltip("Objects disabled while paused and re-enabled while playing - e.g. the Left/Right drumsticks (baguettes).")]
    public GameObject[] hideWhilePaused;

    [Tooltip("Log pause-input diagnostics to the Console. Turn off once the pause button is confirmed working.")]
    public bool logDebug = true;

    bool m_IsPaused;

    void OnEnable()
    {
        InputAction action = pauseAction.action;
        if (action == null)
        {
            Debug.LogWarning("[PauseMenu] pauseAction has no action assigned.", this);
            return;
        }

        action.performed += OnPauseToggle;
        action.Enable();

        if (logDebug)
        {
            Debug.Log($"[PauseMenu] bound '{action.name}' (enabled={action.enabled}, bindings={action.bindings.Count}, resolvedControls={action.controls.Count}).", this);
        }
    }

    void OnDisable()
    {
        InputAction action = pauseAction.action;
        if (action != null)
        {
            // Only detach our handler - the action may be shared with XRI.
            action.performed -= OnPauseToggle;
        }
    }

    void OnPauseToggle(InputAction.CallbackContext ctx)
    {
        if (logDebug)
        {
            Debug.Log($"[PauseMenu] pause pressed (control='{ctx.control?.path}') -> paused will be {!m_IsPaused}.", this);
        }

        Toggle();
    }

    /// <summary>Flip between paused and playing.</summary>
    public void Toggle()
    {
        if (m_IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (m_IsPaused)
        {
            return;
        }

        m_IsPaused = true;
        Time.timeScale = 0f;
        if (conductor != null)
        {
            conductor.Pause();
        }

        if (modeLabel != null)
        {
            modeLabel.SetActive(false);
        }

        ApplyControllerSwap(true);
        ShowPauseMenu();
    }

    public void Resume()
    {
        if (!m_IsPaused)
        {
            return;
        }

        m_IsPaused = false;
        Time.timeScale = 1f;
        if (conductor != null)
        {
            conductor.Resume();
        }

        if (pauseBackground != null)
        {
            pauseBackground.SetActive(false);
        }

        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }

        if (modeLabel != null)
        {
            modeLabel.SetActive(true);
        }

        ApplyControllerSwap(false);
    }

    /// <summary>Swaps the controllers between menu mode (UI ray pointers) and play mode (drumsticks).</summary>
    void ApplyControllerSwap(bool paused)
    {
        if (showWhilePaused != null)
        {
            foreach (GameObject go in showWhilePaused)
            {
                if (go != null)
                {
                    go.SetActive(paused);
                }
            }
        }

        if (hideWhilePaused != null)
        {
            foreach (GameObject go in hideWhilePaused)
            {
                if (go != null)
                {
                    go.SetActive(!paused);
                }
            }
        }
    }

    public void ShowHelp()
    {
        if (pauseBackground != null)
        {
            pauseBackground.SetActive(false);
        }

        if (helpPanel != null)
        {
            helpPanel.SetActive(true);
        }
    }

    public void ShowPauseMenu()
    {
        if (pauseBackground != null)
        {
            pauseBackground.SetActive(true);
        }

        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }
    }

    public void QuitGame()
    {
        // Restore the clock before leaving, otherwise the next scene loads frozen.
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
