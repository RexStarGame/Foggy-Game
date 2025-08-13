using UnityEngine;
using UnityEngine.SceneManagement;

public class FrogMovement : MonoBehaviour
{
    // movement
    private bool isMoving;
    private Vector3 origPos, targetPos;
    public float gridSize = 2f;
    public float timeToMove = 0.2f;

    // UI / VFX
    [SerializeField] GameObject deathMenu;
    [SerializeField] GameObject blood;

    // death state
    bool isDead = false;
    bool isFrozen = false;
    float freezeAtUnscaledTime = -1f;
    float defaultFixedDelta;

    [Header("Death Slow-Mo")]
    public float slowMoScale = 0.15f;   // 15% speed
    public float slowMoSeconds = 0.8f;  // real seconds before full freeze

    // collider/layer handling
    Collider2D[] myCols;
    int defaultLayer;
    int deadLayer; // we'll try "Dead", else fall back to Ignore Raycast (2)

    void Start()
    {
        if (blood) blood.SetActive(false);

        defaultFixedDelta = Time.fixedDeltaTime;
        myCols = GetComponentsInChildren<Collider2D>(includeInactive: true);
        defaultLayer = gameObject.layer;

        deadLayer = LayerMask.NameToLayer("Dead");
        if (deadLayer == -1) deadLayer = LayerMask.NameToLayer("Ignore Raycast"); // layer 2 default project
        if (deadLayer == -1) deadLayer = defaultLayer; // last resort (shouldn’t happen)
    }

    void Update()
    {
        // switch from slow-mo to full freeze when deadline hits (uses REAL time)
        if (isDead && !isFrozen && freezeAtUnscaledTime > 0f && Time.unscaledTime >= freezeAtUnscaledTime)
        {
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            isFrozen = true;
        }

        // restart

        if (isDead) return; // block input while dead

        if (Input.GetKey(KeyCode.W) && !isMoving) StartCoroutine(MovePlayer(Vector3.up));
        if (Input.GetKey(KeyCode.A) && !isMoving) StartCoroutine(MovePlayer(Vector3.left));
        if (Input.GetKey(KeyCode.D) && !isMoving) StartCoroutine(MovePlayer(Vector3.right));
        if (Input.GetKey(KeyCode.S) && !isMoving) StartCoroutine(MovePlayer(Vector3.down));
    }

    private System.Collections.IEnumerator MovePlayer(Vector3 direction)
    {
        isMoving = true;
        float t = 0f;
        origPos = transform.position;
        targetPos = origPos + direction * gridSize;

        while (t < timeToMove)
        {
            transform.position = Vector3.Lerp(origPos, targetPos, t / timeToMove);
            t += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        isMoving = false;
    }
    public void GameOver()
    {
        if (isDead) return; // already dead, ignore further calls
        isDead = true;
        // VFX/UI
        if (blood) blood.SetActive(true);
        if (deathMenu) deathMenu.SetActive(true);
        // stop current motion instantly
        StopAllCoroutines();
        isMoving = false;
        // ❌ Turn off collisions so cars pass through
        foreach (var c in myCols) if (c) c.enabled = false;
        // 🔄 Move entire frog hierarchy to a "Dead" (or Ignore Raycast) layer
        foreach (var t in GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = deadLayer;
        // enter slow-mo, then Update() will freeze after slowMoSeconds
        Time.timeScale = slowMoScale;
        Time.fixedDeltaTime = defaultFixedDelta * Time.timeScale;
        freezeAtUnscaledTime = Time.unscaledTime + slowMoSeconds;
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDead && other.CompareTag("Enemies"))
        {
            isDead = true;

            // VFX/UI
            if (blood) blood.SetActive(true);
            if (deathMenu) deathMenu.SetActive(true);

            // stop current motion instantly
            StopAllCoroutines();
            isMoving = false;

            // ❌ Turn off collisions so cars pass through
            foreach (var c in myCols) if (c) c.enabled = false;

            // 🔄 Move entire frog hierarchy to a "Dead" (or Ignore Raycast) layer
            foreach (var t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = deadLayer;

            // enter slow-mo, then Update() will freeze after slowMoSeconds
            Time.timeScale = slowMoScale;
            Time.fixedDeltaTime = defaultFixedDelta * Time.timeScale;
            freezeAtUnscaledTime = Time.unscaledTime + slowMoSeconds;

        }
    }
    public void RestartGame()
    { 
        // restore (not strictly needed before reload, but safe if you later respawn instead)
        foreach (var c in myCols) if (c) c.enabled = true;

        foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = defaultLayer;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDelta;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        return; 
    }
}
