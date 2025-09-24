using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class DeathScreenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject deathMenu;   // Panel containing death menu UI
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highScoreText;

    [Header("Navigation")]
    [SerializeField] private Button[] deathButtons;  // Order of selectable buttons (Top->Bottom)
    [SerializeField] private int initialIndex = 0;   // Which button to select first

    private int _index = 0;
    private bool _wasMenuActive = false;

    void Start()
    {
        if (deathMenu != null) deathMenu.SetActive(false);

        // If you prefer to auto-fill from children when you forget to assign:
        if ((deathButtons == null || deathButtons.Length == 0) && deathMenu != null)
            deathButtons = deathMenu.GetComponentsInChildren<Button>(true);
    }

    void Update()
    {
        bool isActive = deathMenu != null && deathMenu.activeSelf;
        if (!isActive)
        {
            _wasMenuActive = false;
            return;
        }

        // If the menu just became active (even if not via ShowDeathScreen), auto-focus
        if (!_wasMenuActive)
        {
            _index = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, (deathButtons?.Length ?? 1) - 1));
            EnsureSelection(forceIfNone: true);
            _wasMenuActive = true;
        }

        // --- Navigation input (works at Time.timeScale = 0) ---
        bool upPressed =
            Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool downPressed =
            Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);

        // If user hits W/S but nothing selected, auto-select current index first
        if ((upPressed || downPressed) && !HasSelection())
            EnsureSelection(forceIfNone: true);

        if (upPressed) MoveSelection(-1);
        if (downPressed) MoveSelection(+1);

        // Confirm with P / Enter / Space
        if (Input.GetKeyDown(KeyCode.P) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            ClickCurrent();
        }
    }

    // Show death screen with final score; lock game + mark as dead
    public void ShowDeathScreen(int finalScore)
    {
        if (deathMenu != null) deathMenu.SetActive(true);

        if (scoreText != null)
            scoreText.text = "Score: " + finalScore;

        if (highScoreText != null)
        {
            int highScore = PlayerPrefs.GetInt("HighScore", 0);
            highScoreText.text = "High Score: " + highScore;
        }

        GameState.IsPlayerAlive = false;
        Time.timeScale = 0f; // freeze gameplay while in death menu

        // Ensure we have buttons (in case you forgot to assign)
        if ((deathButtons == null || deathButtons.Length == 0) && deathMenu != null)
            deathButtons = deathMenu.GetComponentsInChildren<Button>(true);

        // Focus initial button
        _index = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, (deathButtons?.Length ?? 1) - 1));
        EnsureSelection(forceIfNone: true);

        _wasMenuActive = true;
    }

    // Call this when leaving the death menu via button (e.g., Reload/Respawn)
    public void HideDeathScreen()
    {
        if (deathMenu != null) deathMenu.SetActive(false);
        Time.timeScale = 1f;
        GameState.IsPlayerAlive = true;
        EventSystem.current?.SetSelectedGameObject(null);
        _wasMenuActive = false;
    }

    private void MoveSelection(int delta)
    {
        if (deathButtons == null || deathButtons.Length == 0) return;

        // Find a valid starting index if current is out of range/null
        if (_index < 0 || _index >= deathButtons.Length || deathButtons[_index] == null)
            _index = FirstValidIndex();
        if (_index == -1) return;

        int count = deathButtons.Length;
        for (int i = 0; i < count; i++)
        {
            _index = (_index + delta) % count;
            if (_index < 0) _index += count;
            if (deathButtons[_index] != null)
            {
                FocusIndex(_index);
                return;
            }
        }
    }

    private void FocusIndex(int i)
    {
        if (deathButtons == null || i < 0 || i >= deathButtons.Length) return;
        var btn = deathButtons[i];
        if (btn == null) return;

        // Ensure EventSystem exists in scene
        if (EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(btn.gameObject);
    }

    private void EnsureSelection(bool forceIfNone)
    {
        if (EventSystem.current == null) return;

        // Already have something selected and it's a Button? keep it.
        var sel = EventSystem.current.currentSelectedGameObject;
        if (!forceIfNone && sel != null && sel.GetComponent<Button>() != null) return;

        // Otherwise focus current index (or first valid)
        int idx = _index;
        if (deathButtons == null || deathButtons.Length == 0)
            return;

        if (idx < 0 || idx >= deathButtons.Length || deathButtons[idx] == null)
            idx = FirstValidIndex();

        if (idx != -1) FocusIndex(idx);
    }

    private bool HasSelection()
    {
        return EventSystem.current != null &&
               EventSystem.current.currentSelectedGameObject != null &&
               EventSystem.current.currentSelectedGameObject.GetComponent<Button>() != null;
    }

    private int FirstValidIndex()
    {
        if (deathButtons == null) return -1;
        for (int i = 0; i < deathButtons.Length; i++)
            if (deathButtons[i] != null) return i;
        return -1;
    }

    private void ClickCurrent()
    {
        if (EventSystem.current != null)
        {
            var sel = EventSystem.current.currentSelectedGameObject;
            if (sel != null && sel.TryGetComponent(out Button btn))
            {
                btn.onClick.Invoke();
                return;
            }
        }

        // Fallback: click current index if nothing is selected
        if (deathButtons != null &&
            _index >= 0 && _index < deathButtons.Length &&
            deathButtons[_index] != null)
        {
            deathButtons[_index].onClick.Invoke();
        }
    }
}
