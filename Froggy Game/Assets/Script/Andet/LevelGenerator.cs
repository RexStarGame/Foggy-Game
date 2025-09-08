using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal chunk-komponent med ankre. Sæt 'entry' i bunden og 'exit' i toppen af dit chunk.
/// </summary>


/// <summary>
/// Drift-fri level generator: next.entry snappes til yCursor (sidste top) + extraGapY.
/// Kræver ankre som standard (kan slås fra), runder Y for at undgå float-drift,
/// auto-spawner foran spilleren og despawner bagved.
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    public Transform entry; // nederste kant
    public Transform exit;  // øverste kant
    [Header("Spawn Mode")]
    [Tooltip("If true, spawns are handled automatically from Update()")]
    public bool useAutoSpawn = true;
    [SerializeField] private float spawnCooldown = 0.05f; // sikring mod dobbelt-spawn
    private float lastSpawnTime = -999f;

    [Header("Setup")]
    [Tooltip("Prefabs der hver indeholder et LevelChunk (på sig selv eller child).")]
    public GameObject[] chunkPrefabs;
    [Tooltip("Placering af første chunk (valgfri).")]
    public Transform spawnPointA;
    [Tooltip("Spillerens transform (bruges til auto-spawn/despawn).")]
    public Transform player;

    [Header("Placement")]
    [Tooltip("Lodret afstand mellem chunks (oven på exit->entry justering).")]
    public float extraGapY = 0f;
    [Tooltip("Lås alle chunks til samme X-koordinat.")]
    public bool lockX = true;
    [Tooltip("X-koordinat til lock.")]
    public float lockXValue = 0f;

    [Header("Lifetime")]
    [Tooltip("Hvor mange chunks holdes i live-køen.")]
    public int keepChunks = 6;
    [Tooltip("Despawn når spilleren er så langt over chunk-top (Y).")]
    public float despawnBehindDistance = 40f;
    [Tooltip("Spawn når spilleren er så tæt på sidste top (Y).")]
    public float spawnAheadDistance = 25f;

    [Header("Validation")]
    [Tooltip("If true, refuse to spawn chunks that don't have entry+exit anchors.")]
    public bool requireAnchors = true;
    [Tooltip("Round final Y positions to this step to avoid float drift.")]
    public float roundStep = 0.001f;

    // Runtime
    private readonly Queue<LevelChunk> live = new Queue<LevelChunk>();
    private LevelChunk lastChunk;
    private bool quitting;
    private float yCursor; // præcis top-Y for sidste chunk (drift-fri lineal)

    private void OnApplicationQuit() => quitting = true;

    private void Start()
    {
        keepChunks = Mathf.Max(1, keepChunks);

        if (chunkPrefabs == null || chunkPrefabs.Length == 0)
        {
            Debug.LogError("[LevelGenerator] No chunkPrefabs assigned.");
            enabled = false;
            return;
        }

        // ---- Første chunk ----
        Vector3 startPos = spawnPointA ? spawnPointA.position : Vector3.zero;

        GameObject firstGO = Instantiate(GetRandomPrefab(), startPos, Quaternion.identity);
        if (!TryGetChunk(firstGO, out var first))
        {
            Debug.LogError("[LevelGenerator] First chunk missing LevelChunk.");
            Destroy(firstGO);
            enabled = false;
            return;
        }

        if (requireAnchors && first.entry == null)
        {
            Debug.LogError("[LevelGenerator] First chunk missing 'entry' while requireAnchors = true.");
            Destroy(firstGO);
            enabled = false;
            return;
        }

        // Justér så entry rammer startPos.y, ellers brug bund-bounds
        if (first.entry != null)
        {
            var p = firstGO.transform.position;
            p.y += (startPos.y - first.entry.position.y);
            firstGO.transform.position = p;
        }
        else
        {
            AlignBottomToY(firstGO.transform, startPos.y);
        }

        RoundY(firstGO.transform, roundStep);
        if (lockX) SetX(firstGO.transform, lockXValue);

        live.Enqueue(first);
        lastChunk = first;
        yCursor = GetTopY(first); // cursor = præcis første top

        // Seed et par stykker mere
        int seed = Mathf.Clamp(keepChunks - 1, 0, 3);
        for (int i = 0; i < seed; i++) SpawnNextByCursor();
        lastSpawnTime = Time.time;
    }

    private void Update()
    {
        if (!player) return;

        // DESPAWN – fjern ældste bag spilleren
        if (live.Count > 0)
        {
            var oldest = live.Peek();
            if (!oldest) live.Dequeue();
            else
            {
                float oldestTop = GetTopY(oldest);
                if (player.position.y - oldestTop > despawnBehindDistance)
                {
                    live.Dequeue();
                    if (oldest) Destroy(oldest.gameObject);
                }
            }
        }

        // AUTO-SPAWN – læg nyt foran spilleren
        if (useAutoSpawn && lastChunk)
        {
            if (player.position.y + spawnAheadDistance > yCursor &&
                Time.time - lastSpawnTime >= spawnCooldown)
            {
                SpawnNextByCursor();
                lastSpawnTime = Time.time;
            }
        }
    }

    // ---------- Public: manuel spawn hvis du vil kalde den fra UI/Debug ----------
    public void SpawnNext()
    {
        SpawnNextByCursor();
        lastSpawnTime = Time.time;
    }

    // ---------- Intern: drift-fri spawn baseret på yCursor ----------
    private void SpawnNextByCursor()
    {
        if (quitting) return;

        GameObject prefab = GetRandomPrefab();
        if (!prefab)
        {
            Debug.LogWarning("[LevelGenerator] GetRandomPrefab returned null.");
            return;
        }

        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        if (!TryGetChunk(go, out var next))
        {
            Debug.LogError("[LevelGenerator] Spawned prefab has no LevelChunk.");
            Destroy(go);
            return;
        }

        if (requireAnchors && next.entry == null)
        {
            Debug.LogError("[LevelGenerator] Missing entry on spawned chunk while requireAnchors = true.");
            Destroy(go);
            return;
        }

        // Placer så next.entry (eller bund-bounds) = yCursor + extraGapY
        float targetBottomY = yCursor + extraGapY;

        if (next.entry != null)
        {
            var pos = go.transform.position;
            pos.y += (targetBottomY - next.entry.position.y);
            go.transform.position = pos;
        }
        else
        {
            Bounds nb = GetWorldBounds(go.transform);
            float currentBottom = (nb.size != Vector3.zero) ? nb.min.y : go.transform.position.y;
            float dy = targetBottomY - currentBottom;
            go.transform.position += new Vector3(0f, dy, 0f);
        }

        RoundY(go.transform, roundStep);
        if (lockX) SetX(go.transform, lockXValue);

        live.Enqueue(next);
        lastChunk = next;

        // Opdatér cursoren til chunkets præcise top
        yCursor = GetTopY(next);

        // Trim køen
        while (live.Count > keepChunks)
        {
            var old = live.Dequeue();
            if (old) Destroy(old.gameObject);
        }
    }

    // ---------------- Hjælpere ----------------
    private GameObject GetRandomPrefab()
    {
        if (chunkPrefabs == null || chunkPrefabs.Length == 0) return null;

        // vælg en ikke-null prefab; tolerér null-slots
        for (int i = 0; i < 8; i++)
        {
            var p = chunkPrefabs[Random.Range(0, chunkPrefabs.Length)];
            if (p) return p;
        }
        foreach (var p in chunkPrefabs) if (p) return p;
        return null;
    }

    private static bool TryGetChunk(GameObject go, out LevelChunk chunk)
    {
        chunk = null;
        if (!go) return false;
        chunk = go.GetComponent<LevelChunk>() ?? go.GetComponentInChildren<LevelChunk>();
        return chunk != null;
    }

    private static void AlignBottomToY(Transform t, float targetY)
    {
        Bounds b = GetWorldBounds(t);
        if (b.size == Vector3.zero) return;
        float dy = targetY - b.min.y;
        t.position += new Vector3(0f, dy, 0f);
    }

    private static void SetX(Transform t, float x)
    {
        var p = t.position; p.x = x; t.position = p;
    }

    private static float GetTopY(LevelChunk chunk)
    {
        if (chunk.exit != null) return chunk.exit.position.y;
        Bounds b = GetWorldBounds(chunk.transform);
        if (b.size != Vector3.zero) return b.max.y;
        return chunk.transform.position.y;
    }

    private static float RoundToStep(float v, float step) => Mathf.Round(v / step) * step;

    private static void RoundY(Transform t, float step)
    {
        var p = t.position;
        p.y = RoundToStep(p.y, step);
        t.position = p;
    }

    /// <summary>World-bounds fra Renderers, ellers Colliders, ellers child-positions.</summary>
    private static Bounds GetWorldBounds(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        var cols = root.GetComponentsInChildren<Collider>(true);
        if (cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b;
        }

        var trs = root.GetComponentsInChildren<Transform>(true);
        if (trs.Length > 1)
        {
            Bounds b = new Bounds(trs[0].position, Vector3.zero);
            for (int i = 1; i < trs.Length; i++) b.Encapsulate(trs[i].position);
            return b;
        }

        return new Bounds(Vector3.zero, Vector3.zero);
    }
}
