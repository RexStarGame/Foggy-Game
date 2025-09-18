using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("Assign the panel that holds the pause UI")]
    public GameObject pausePanel;

    [Tooltip("First button to highlight when the game pauses")]
    public Button firstSelected;

    [Tooltip("Optional: also pause all AudioListeners")]
    public bool pauseAudio = true;

    bool isPaused;

    void Update()
    {
        // --- P pressed? ---
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (!isPaused)
            {
                TogglePause();                // pause the game
            }
            else
            {
                // If paused: treat P as “Submit/Click” on current selection
                ClickCurrentSelection();
            }
        }
    }

    /* ------------------------------------------------- */
    /*                    PUBLIC API                     */
    /* ------------------------------------------------- */

    public void TogglePause()
    {
        isPaused = !isPaused;

        Time.timeScale = isPaused ? 0f : 1f;
        if (pausePanel) pausePanel.SetActive(isPaused);
        if (pauseAudio) AudioListener.pause = isPaused;

        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;

        if (isPaused)
            FocusFirstButton();
        else
            ClearUIFocus();
    }

    public void Resume() => TogglePause();

    public void QuitToDesktop()
    {
        TogglePause();
        Application.Quit();
    }

    public void ReloadScene()
    {
        TogglePause();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /* ------------------------------------------------- */
    /*                 INTERNAL HELPERS                  */
    /* ------------------------------------------------- */

    void FocusFirstButton()
    {
        // Fald tilbage hvis du har glemt at tildele i Inspector
        if (!firstSelected)
            firstSelected = pausePanel.GetComponentInChildren<Button>();

        if (firstSelected)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
    }

    void ClearUIFocus() =>
        EventSystem.current.SetSelectedGameObject(null);

    void ClickCurrentSelection()
    {
        var sel = EventSystem.current.currentSelectedGameObject;
        if (sel != null && sel.TryGetComponent(out Button btn))
        {
            btn.onClick.Invoke();
        }
        else
        {
            // Ingen UI valgt => brug P som “unpause”
            TogglePause();
        }
    }
}
