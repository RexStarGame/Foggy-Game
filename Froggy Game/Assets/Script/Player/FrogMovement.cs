using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class FrogMovement : MonoBehaviour
{
    // -------- Global grid (single source of truth) ----------
    [Header("Global Grid")]
    [Tooltip("World-space size of each cell.")]
    public float gridSize = 2f;
    [Tooltip("World-space origin of the grid (move this to shift the grid).")]
    public Vector2 gridOrigin = Vector2.zero;

    // -------- Movement ----------
    private bool isMoving;
    private Vector3 origPos, targetPos;
    public float timeToMove = 0.2f;

    // -------- Score / UI / VFX ----------
    [SerializeField] private PlayerScore playerScore;
    [SerializeField] private DeathScreenUI deathScreenUI;
    [SerializeField] private GameObject flyCollectParticles;
    [SerializeField] private GameObject blood;

    // -------- Death state ----------
    bool isDead = false;
    bool isFrozen = false;
    float freezeAtUnscaledTime = -1f;
    float defaultFixedDelta;

    [Header("Death Slow-Mo")]
    public float slowMoScale = 0.15f;
    public float slowMoSeconds = 0.8f;

    // -------- Colliders / Layers ----------
    Collider2D[] myCols;
    int defaultLayer;
    int deadLayer; // "Dead" layer if present else Ignore Raycast

    private Animator animator;

    bool onLog = false;

    // -------- Grid overlay (draws the SAME grid) ----------
    [Header("Grid Overlay (visual only)")]
    [SerializeField] private bool gridShow = true;
    [SerializeField] private KeyCode gridToggleKey = KeyCode.G;
    [SerializeField] private Camera gridCamera;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color axisColor = new Color(1f, 1f, 0f, 0.8f); // axes at gridOrigin
    [SerializeField] private bool drawBounds = true;
    [SerializeField] private float boundMinX = -11.5f;
    [SerializeField] private float boundMaxX = 11.5f;
    [SerializeField] private float boundMinY = -6.0f;
    [SerializeField] private Color boundsColor = new Color(1f, 0f, 0f, 0.85f);

    static Material s_gridMat;
    bool gridUsingSRP;

    // ===== Unity lifecycle =====
    void OnEnable()
    {
        EnsureGridMaterial();
        gridUsingSRP = GraphicsSettings.currentRenderPipeline != null;
        if (gridUsingSRP) RenderPipelineManager.endCameraRendering += Grid_OnEndCameraRendering;
        if (gridCamera == null) gridCamera = Camera.main;
    }

    void OnDisable()
    {
        if (gridUsingSRP) RenderPipelineManager.endCameraRendering -= Grid_OnEndCameraRendering;
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        if (blood) blood.SetActive(false);

        defaultFixedDelta = Time.fixedDeltaTime;
        myCols = GetComponentsInChildren<Collider2D>(includeInactive: true);
        defaultLayer = gameObject.layer;

        deadLayer = LayerMask.NameToLayer("Dead");
        if (deadLayer == -1) deadLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (deadLayer == -1) deadLayer = defaultLayer;
    }

    void Update()
    {
        if (Input.GetKeyDown(gridToggleKey)) gridShow = !gridShow;

        // Death freeze timing (real time)
        if (isDead && !isFrozen && freezeAtUnscaledTime > 0f && Time.unscaledTime >= freezeAtUnscaledTime)
        {
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            isFrozen = true;
        }

        if (isDead) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            animator.SetTrigger("Tongue");
            return; // stop here so it doesn’t also trigger movement
        }
        // WASD movement (keep your original key mapping)
        if (Input.GetKey(KeyCode.A) && !isMoving) StartCoroutine(MovePlayer(Vector3.up));
        if (Input.GetKey(KeyCode.W) && !isMoving) StartCoroutine(MovePlayer(Vector3.left));
        if (Input.GetKey(KeyCode.S) && !isMoving) StartCoroutine(MovePlayer(Vector3.right));
        if (Input.GetKey(KeyCode.D) && !isMoving) StartCoroutine(MovePlayer(Vector3.down));


    }

    // ===== Movement on the global grid =====
    private System.Collections.IEnumerator MovePlayer(Vector3 direction)
    {
        isMoving = true;

        if (animator)
        {
            animator.SetBool("JumpMove", true);
            animator.SetBool("Idle", false);
        }

        // Face the hop direction
        if (direction == Vector3.up) transform.rotation = Quaternion.Euler(0, 0, 0);
        else if (direction == Vector3.down) transform.rotation = Quaternion.Euler(0, 0, 180);
        else if (direction == Vector3.left) transform.rotation = Quaternion.Euler(0, 0, 90);
        else if (direction == Vector3.right) transform.rotation = Quaternion.Euler(0, 0, -90);

        // Convert current position to grid index (relative to gridOrigin)
        Vector2Int curIdx = WorldToIndex(transform.position);

        // Direction → index step
        Vector2Int step = Vector2Int.zero;
        if (direction == Vector3.up) step = Vector2Int.up;
        else if (direction == Vector3.down) step = Vector2Int.down;
        else if (direction == Vector3.left) step = Vector2Int.left;
        else if (direction == Vector3.right) step = Vector2Int.right;

        // Candidate next index
        Vector2Int nextIdx = curIdx + step;

        // Clamp by bounds **on indices** so we stay on the grid but inside world limits
        int minIdxX = Mathf.CeilToInt((boundMinX - gridOrigin.x) / gridSize);
        int maxIdxX = Mathf.FloorToInt((boundMaxX - gridOrigin.x) / gridSize);
        int minIdxY = Mathf.CeilToInt((boundMinY - gridOrigin.y) / gridSize);

        nextIdx.x = Mathf.Clamp(nextIdx.x, minIdxX, maxIdxX);
        nextIdx.y = Mathf.Max(nextIdx.y, minIdxY);

        // Convert back to world
        Vector3 nextWorld = IndexToWorld(nextIdx);

        // Smooth hop
        float t = 0f;
        origPos = transform.position;
        targetPos = nextWorld;

        while (t < timeToMove)
        {
            transform.position = Vector3.Lerp(origPos, targetPos, t / timeToMove);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        if (playerScore != null) playerScore.TryAddScore(transform.position);

        if (animator)
        {
            animator.SetBool("JumpMove", false);
            animator.SetBool("Idle", true);
        }

        isMoving = false;

        CheckIfSafe();
    }

    Vector2Int WorldToIndex(Vector3 world)
    {
        // Round so you stay on exact grid slots
        int ix = Mathf.RoundToInt((world.x - gridOrigin.x) / gridSize);
        int iy = Mathf.RoundToInt((world.y - gridOrigin.y) / gridSize);
        return new Vector2Int(ix, iy);
    }

    Vector3 IndexToWorld(Vector2Int idx)
    {
        float x = gridOrigin.x + idx.x * gridSize;
        float y = gridOrigin.y + idx.y * gridSize;
        return new Vector3(x, y, transform.position.z);
    }

    // ===== Safety / death =====
    private void CheckIfSafe()
    {
        float checkRadius = 0.7f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, checkRadius);

        bool onSafeGround = false;
        bool touchingLog = false;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Ground") || hit.CompareTag("SafeZone")) onSafeGround = true;
            if (hit.CompareTag("Log")) touchingLog = true;
        }

        if (onSafeGround || touchingLog || onLog) return;
        GameOver();
    }

    public void GameOver()
    {
        if (isDead) return;
        isDead = true;

        if (blood) blood.SetActive(true);
        GameState.IsPlayerAlive = false;   // <- tells PauseManager to block pause
        StopAllCoroutines();
        isMoving = false;

        foreach (var c in myCols) if (c) c.enabled = false;
        foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = deadLayer;

        if (deathScreenUI != null && playerScore != null)
            deathScreenUI.ShowDeathScreen(playerScore.score);

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
            transform.SetParent(other.transform); // move with log
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Log"))
        {
            onLog = false;

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
            transform.SetParent(null);
        }
    }

    private void CollectFly(GameObject fly)
    {
        if (playerScore != null) playerScore.AddScore(250);

        if (flyCollectParticles != null)
        {
            GameObject particles = Instantiate(flyCollectParticles, fly.transform.position, Quaternion.identity);
            Destroy(particles, 2f);
        }

        Destroy(fly);
    }

    public void RestartGame()
    {
        foreach (var c in myCols) if (c) c.enabled = true;
        foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = defaultLayer;
        GameState.IsPlayerAlive = true;   // <- tells PauseManager to block pause
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDelta;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ============================
    // ==== GRID RENDER (same) ====
    // ============================
    void OnRenderObject() // Built-in RP
    {
        if (gridUsingSRP) return;
        if (!GridValidate()) return;
        if (Camera.current != gridCamera) return;
        DrawGrid(gridCamera);
    }

    void Grid_OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam) // URP/HDRP
    {
        if (cam != gridCamera) return;
        if (!GridValidate()) return;
        DrawGrid(cam);
    }

    bool GridValidate()
    {
        if (!gridShow) return false;
        if (gridCamera == null) gridCamera = Camera.main;
        if (gridCamera == null) return false;
        if (!gridCamera.orthographic) return false;
        EnsureGridMaterial();
        if (s_gridMat == null) return false;
        return gridSize > 0f;
    }

    void DrawGrid(Camera cam)
    {
        // camera rect in world
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector3 cc = cam.transform.position;

        float minX = cc.x - w * 0.5f;
        float maxX = cc.x + w * 0.5f;
        float minY = cc.y - h * 0.5f;
        float maxY = cc.y + h * 0.5f;

        // align to global grid origin
        float startX = minX - Mathf.Repeat(minX - gridOrigin.x, gridSize);
        float startY = minY - Mathf.Repeat(minY - gridOrigin.y, gridSize);

        GL.PushMatrix();
        GL.LoadProjectionMatrix(cam.projectionMatrix);
        GL.modelview = cam.worldToCameraMatrix;

        s_gridMat.SetPass(0);
        GL.Begin(GL.LINES);

        // vertical lines
        for (float x = startX; x <= maxX + 0.0001f; x += gridSize)
        {
            bool isAxis = Mathf.Abs(x - gridOrigin.x) < 0.0001f;
            GL.Color(isAxis ? axisColor : lineColor);
            GL.Vertex3(x, minY, 0f);
            GL.Vertex3(x, maxY, 0f);
        }

        // horizontal lines
        for (float y = startY; y <= maxY + 0.0001f; y += gridSize)
        {
            bool isAxis = Mathf.Abs(y - gridOrigin.y) < 0.0001f;
            GL.Color(isAxis ? axisColor : lineColor);
            GL.Vertex3(minX, y, 0f);
            GL.Vertex3(maxX, y, 0f);
        }

        // optional bounds
        if (drawBounds)
        {
            GL.Color(boundsColor);
            GL.Vertex3(boundMinX, minY, 0f); GL.Vertex3(boundMinX, maxY, 0f);
            GL.Vertex3(boundMaxX, minY, 0f); GL.Vertex3(boundMaxX, maxY, 0f);
            GL.Vertex3(minX, boundMinY, 0f); GL.Vertex3(maxX, boundMinY, 0f);
        }

        GL.End();
        GL.PopMatrix();
    }

    static void EnsureGridMaterial()
    {
        if (s_gridMat != null) return;

        var shader = Shader.Find("Hidden/Internal-Colored");
        if (!shader) shader = Shader.Find("Sprites/Default");
        if (!shader) return;

        s_gridMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        s_gridMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        s_gridMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        s_gridMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        s_gridMat.SetInt("_ZWrite", 0);
        if (s_gridMat.HasProperty("_ZTest"))
            s_gridMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }
}
