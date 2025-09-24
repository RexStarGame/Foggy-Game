using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drift-free level generator: aligns next chunk's entry to yCursor (last top) + extraGapY,
/// rounds Y to avoid float drift, auto-spawns ahead of the player and despawns behind.
/// Requires chunks providing a LevelChunk component (which itself exposes 'entry' & 'exit').
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    [Header("Spawn Mode")]
    [Tooltip("If true, spawns are handled automatically from Update()")]
    public bool useAutoSpawn = true;
    [SerializeField] private float spawnCooldown = 0.05f;
    private float lastSpawnTime = -999f;

    [Header("Setup")]
    [Tooltip("Prefabs that each contain a LevelChunk (on self or a child).")]
    public GameObject[] chunkPrefabs;
    [Tooltip("Optional initial placement for the first chunk.")]
    public Transform spawnPointA;
    [Tooltip("Player transform (used for auto-spawn/despawn).")]
    public Transform player;

    [Header("Placement")]
    [Tooltip("Vertical gap added between chunks.")]
    public float extraGapY = 0f;
    [Tooltip("Lock all chunks to the same X coordinate.")]
    public bool lockX = true;
    [Tooltip("X coordinate used when lockX is true.")]
    public float lockXValue = 0f;

    [Header("Lifetime")]
    [Tooltip("How many chunks to keep alive in the queue.")]
    public int keepChunks = 6;
    [Tooltip("Despawn when player is this far above a chunk's top (Y).")]
    public float despawnBehindDistance = 40f;
    [Tooltip("Spawn when player is this close to last top (Y).")]
    public float spawnAheadDistance = 25f;

    [Header("Validation")]
    [Tooltip("If true, refuse to spawn chunks that don't have entry+exit anchors.")]
    public bool requireAnchors = true;
    [Tooltip("Round final Y positions to this step to avoid float drift. Set <= 0 to disable.")]
    public float roundStep = 0.001f;

    // Runtime
    private readonly Queue<LevelChunk> live = new Queue<LevelChunk>();
    private LevelChunk lastChunk;
    private bool quitting;
    private float yCursor; // precise top-Y for last chunk (drift-free ruler)

    private void OnApplicationQuit() => quitting = true;

    private void Start()
    {
        keepChunks = Mathf.Max(1, keepChunks);

        if (chunkPrefabs == null || chunkPrefabs.Length == 0)
        {
            enabled = false;
            return;
        }

        // ---- First chunk ----
        Vector3 startPos = spawnPointA ? spawnPointA.position : Vector3.zero;

        GameObject firstGO = Instantiate(GetRandomPrefab(), startPos, Quaternion.identity);
        if (!TryGetChunk(firstGO, out var first))
        {
            Destroy(firstGO);
            enabled = false;
            return;
        }

        if (requireAnchors && first.entry == null)
        {
            Destroy(firstGO);
            enabled = false;
            return;
        }

        // Align so entry hits startPos.y, else use bottom bounds
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
        yCursor = GetTopY(first);

        // Seed a couple more
        int seed = Mathf.Clamp(keepChunks - 1, 0, 3);
        for (int i = 0; i < seed; i++) SpawnNextByCursor();
        lastSpawnTime = Time.time;
    }

    private void Update()
    {
        if (!player) return;

        // DESPAWN – remove oldest behind the player
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

        // AUTO-SPAWN – place new ahead of player
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

    // Public: manual spawn if you want to call it from UI
    public void SpawnNext()
    {
        SpawnNextByCursor();
        lastSpawnTime = Time.time;
    }

    // Internal: drift-free spawn based on yCursor
    private void SpawnNextByCursor()
    {
        if (quitting) return;

        GameObject prefab = GetRandomPrefab();
        if (!prefab) return;

        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        if (!TryGetChunk(go, out var next))
        {
            Destroy(go);
            return;
        }

        if (requireAnchors && next.entry == null)
        {
            Destroy(go);
            return;
        }

        // Place so next.entry (or bottom-bounds) = yCursor + extraGapY
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

        // Update cursor to the precise top of this chunk
        yCursor = GetTopY(next);

        // Trim queue
        while (live.Count > keepChunks)
        {
            var old = live.Dequeue();
            if (old) Destroy(old.gameObject);
        }
    }

    // ---------------- Helpers ----------------
    private GameObject GetRandomPrefab()
    {
        if (chunkPrefabs == null || chunkPrefabs.Length == 0) return null;

        // pick a non-null prefab; tolerate null slots
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

    private static float RoundToStep(float v, float step)
    {
        if (step <= 0f) return v;
        return Mathf.Round(v / step) * step;
    }

    private static void RoundY(Transform t, float step)
    {
        var p = t.position;
        p.y = RoundToStep(p.y, step);
        t.position = p;
    }

    /// <summary>World-bounds from Renderers, else Colliders, else child positions.</summary>
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
