using UnityEngine;
using UnityEngine.AdaptivePerformance.Provider;
using UnityEngine.SceneManagement;

public class FrogMovement : MonoBehaviour
{
    // movement
    private bool isMoving;
    private Vector3 origPos, targetPos;
    public float gridSize = 2f;
    public float timeToMove = 0.2f;

    // Water level
    bool isInWater = false;
    bool onLog = false;

    // Score
    [SerializeField] private PlayerScore playerScore;
    [SerializeField] private DeathScreenUI deathScreenUI;
    [SerializeField] private GameObject flyCollectParticles;

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

    Animator move;
    void Start()
    {
        move = GetComponent<Animator>();
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

        if (Input.GetKey(KeyCode.A) && !isMoving) StartCoroutine(MovePlayer(Vector3.up));
        if (Input.GetKey(KeyCode.W) && !isMoving) StartCoroutine(MovePlayer(Vector3.left));
        if (Input.GetKey(KeyCode.S) && !isMoving) StartCoroutine(MovePlayer(Vector3.right));
        if (Input.GetKey(KeyCode.D) && !isMoving) StartCoroutine(MovePlayer(Vector3.down));
    }

    private System.Collections.IEnumerator MovePlayer(Vector3 direction)
    {
        isMoving = true;

        // Set animation state to moving
        move.SetBool("JumpMove", true);
        move.SetBool("Idle", false);

        // Set facing direction
        if (direction == Vector3.up)
            transform.rotation = Quaternion.Euler(0, 0, 0);
        else if (direction == Vector3.down)
            transform.rotation = Quaternion.Euler(0, 0, 180);
        else if (direction == Vector3.left)
            transform.rotation = Quaternion.Euler(0, 0, 90);
        else if (direction == Vector3.right)
            transform.rotation = Quaternion.Euler(0, 0, -90);

        float t = 0f;
        origPos = transform.position;
        targetPos = origPos + direction * gridSize;
        
        targetPos.x = Mathf.Clamp(targetPos.x, -11.5f, 11.5f);

        float maxValue = Mathf.Infinity;
        float minValue = -6.0f;
        targetPos.y = Mathf.Clamp(targetPos.y, minValue,maxValue );

        while (t < timeToMove)
        {
            transform.position = Vector3.Lerp(origPos, targetPos, t / timeToMove);
            t += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;

        //+ update score after finishing a move
        if (playerScore != null)
        {
            playerScore.TryAddScore(transform.position);
        }

        // Stop moving → back to idle
        move.SetBool("JumpMove", false);
        move.SetBool("Idle", true);

        isMoving = false;

        CheckIfSafe();

        // Stop moving → back to idle
        move.SetBool("JumpMove", false);
        move.SetBool("Idle", true);

        isMoving = false;
    }
    private void CheckIfSafe()
    {
        float checkRadius = 0.7f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, checkRadius);

        bool onSafeGround = false;
        bool touchingLog = false;

        foreach (var hit in hits)
        {
            Debug.Log("Frog is touching: " + hit.name + " with tag " + hit.tag);

            if (hit.CompareTag("Ground") || hit.CompareTag("SafeZone"))
                onSafeGround = true;

            if (hit.CompareTag("Log"))
                touchingLog = true;
        }

        // Logs take priority over water
        if (onSafeGround || touchingLog || onLog)
            return;

        GameOver();
    }


    public void GameOver()
    {
        if (isDead) return;
        isDead = true;

        // VFX/UI
        if (blood) blood.SetActive(true);

        // Stop motion
        StopAllCoroutines();
        isMoving = false;
        // Turn off collisions so cars pass through
        foreach (var c in myCols) if (c) c.enabled = false;
        // Move entire frog hierarchy to a "Dead" (or Ignore Raycast) layer
        foreach (var t in GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = deadLayer;

        // Show death screen with the final score
        if (deathScreenUI != null && playerScore != null)
            deathScreenUI.ShowDeathScreen(playerScore.score);

        // Slow-motion effect
        Time.timeScale = slowMoScale;
        Time.fixedDeltaTime = defaultFixedDelta * Time.timeScale;
        freezeAtUnscaledTime = Time.unscaledTime + slowMoSeconds;
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDead && other.CompareTag("Enemies"))
        {
            GameOver();
        }
        else if (other.CompareTag("Fly"))
        {
            CollectFly(other.gameObject);
        }
        else if (other.CompareTag("Log"))
        {
            onLog = true;

            // Optional: parent to log to move with it
            transform.SetParent(other.transform);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Log"))
        {
            onLog = false;

            // Check if we’re still touching another log before unparenting
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.7f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Log"))
                {
                    transform.SetParent(hit.transform);
                    onLog = true;
                    return;
                }
            }

            // If no other logs → unparent
            transform.SetParent(null);
        }
    }


    private void CollectFly(GameObject fly)
    {
        if (playerScore != null)
        {
            playerScore.AddScore(250); // add 250 score
        }

        if (flyCollectParticles != null)
        {
            GameObject particles = Instantiate(flyCollectParticles, fly.transform.position, Quaternion.identity);
            Destroy(particles, 2f); // clean up after a short delay
        }

        Destroy(fly); // remove the fly from the scene
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
