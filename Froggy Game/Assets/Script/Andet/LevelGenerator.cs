using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [Header("Setup")]
    public GameObject[] chunkPrefabs;
    public Transform spawnPointA;   // hvor første chunk placeres (valgfrit)
    public Transform player;

    [Header("Placement")]
    public float extraGapY = 0f;        // afstand mellem chunks i Y
    public bool alternateFlip = false;  // spejl hver anden for variation

    [Header("Lifetime")]
    public int keepChunks = 6;              
    public float despawnBehindDistance = 40f;

    private readonly Queue<LevelChunk> live = new Queue<LevelChunk>();
    private bool flipNext = false;
    private bool quitting = false;

    private void OnApplicationQuit() => quitting = true;

    private void Start()
    {
        if (chunkPrefabs == null || chunkPrefabs.Length == 0)
        {
            Debug.LogError("[LevelGenerator] No chunkPrefabs assigned.");
            return;
        }

        // Spawn første chunk ved spawnPointA (eller Vector3.zero)
        Vector3 startPos = spawnPointA ? spawnPointA.position : Vector3.zero;
        var firstGO = Instantiate(GetRandomPrefab(), startPos, Quaternion.identity);
        var first = firstGO.GetComponent<LevelChunk>() ?? firstGO.GetComponentInChildren<LevelChunk>();
        if (first == null)
        {
            Debug.LogError("[LevelGenerator] First chunk missing LevelChunk on root/children.");
            if (firstGO) Destroy(firstGO);
            return;
        }

        // Sikr at første chunk står præcis med sin bund på startPos.y (så kæden starter pænt)
        Bounds firstB = GetWorldBounds(firstGO.transform);
        if (firstB.size != Vector3.zero)
        {
            float bottomY = firstB.min.y;
            float dy = startPos.y - bottomY;
            firstGO.transform.position += new Vector3(0f, dy, 0f);
        }

        live.Enqueue(first);

        // Seed 2-3 ekstra chunks så du kan se banen med det samme
        int seed = Mathf.Clamp(keepChunks - 1, 0, 3);
        var current = first;
        for (int i = 0; i < seed; i++)
        {
            SpawnNext(current);
            var arr = live.ToArray();
            current = arr[arr.Length - 1];
        }
    }

    public void SpawnNext(LevelChunk from)
    {
        if (quitting || from == null) return;

        var go = Instantiate(GetRandomPrefab());
        var next = go.GetComponent<LevelChunk>() ?? go.GetComponentInChildren<LevelChunk>();
        if (next == null)
        {
            Debug.LogError("[LevelGenerator] Spawned prefab has no LevelChunk.");
            Destroy(go);
            return;
        }

        // Flip FØR vi måler bounds/alignment
        if (alternateFlip && flipNext) MirrorX(go.transform);
        flipNext = alternateFlip ? !flipNext : flipNext;

        // MÅL forrige top og næste bund
        Bounds fromB = GetWorldBounds(from.transform);
        Bounds nextB = GetWorldBounds(go.transform);

        if (fromB.size == Vector3.zero || nextB.size == Vector3.zero)
        {
            Debug.LogWarning("[LevelGenerator] Bounds not found on one of the chunks; falling back to entry/exit if present.");

            // Fallback: brug LevelChunk entry/exit, hvis de findes
            if (from.exit != null && next.entry != null)
            {
                Vector3 target = from.exit.position + Vector3.up * extraGapY;
                Vector3 delta = target - next.entry.position;
                go.transform.position += delta;
            }
            else
            {
                // Sidste fallback: placer bare ved forrige tops Y
                float targetY = (from.exit ? from.exit.position.y : from.transform.position.y) + extraGapY;
                float dy = targetY - go.transform.position.y;
                go.transform.position += new Vector3(0f, dy, 0f);
            }
        }
        else
        {
            // Align: næste bund = forrige top + extraGapY
            float fromTopY = fromB.max.y;
            float nextBottomY = nextB.min.y;

            float dy = (fromTopY + extraGapY) - nextBottomY;
            go.transform.position += new Vector3(0f, dy, 0f);

            // (valgfrit) hold X justeret til forrige centers X, så kolonnen er lige
            // float dx = fromB.center.x - GetWorldBounds(go.transform).center.x;
            // go.transform.position += new Vector3(dx, 0f, 0f);
        }

        live.Enqueue(next);
        while (live.Count > keepChunks)
        {
            var old = live.Dequeue();
            if (old) Destroy(old.gameObject);
        }
    }

    public void SpawnNewLevel(Transform fromTransform)
    {
        LevelChunk from = null;
        if (fromTransform)
        {
            from = fromTransform.GetComponent<LevelChunk>();
            if (!from) from = fromTransform.GetComponentInParent<LevelChunk>();
        }

        if (!from)
        {
            Debug.LogWarning("[LevelGenerator] SpawnNewLevel called but no LevelChunk found.");
            return;
        }
        SpawnNext(from);
    }

    private GameObject GetRandomPrefab()
    {
        if (chunkPrefabs == null || chunkPrefabs.Length == 0) return null;
        return chunkPrefabs[Random.Range(0, chunkPrefabs.Length)];
    }

    private void Update()
    {
        if (player && live.Count > 0)
        {
            var oldest = live.Peek();
            if (!oldest) { live.Dequeue(); return; }

            // Brug bounds top til despawn (når spilleren er langt over den)
            Bounds ob = GetWorldBounds(oldest.transform);
            float referenceY = ob.size != Vector3.zero
                ? ob.max.y
                : (oldest.exit ? oldest.exit.position.y : oldest.transform.position.y);

            if (player.position.y - referenceY > despawnBehindDistance)
            {
                live.Dequeue();
                Destroy(oldest.gameObject);
            }
        }
    }

    private static void MirrorX(Transform t)
    {
        var s = t.localScale;
        s.x = -Mathf.Abs(s.x);
        t.localScale = s;
    }

    /// <summary>
    /// Finder samlede world-bounds for et chunk:
    /// 1) Alle Renderers
    /// 2) Ellers alle Colliders
    /// 3) Ellers alle børns positioner
    /// Returnerer Bounds med size=0 hvis intet findes.
    /// </summary>
    private static Bounds GetWorldBounds(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        var cols = root.GetComponentsInChildren<Collider>(true);
        if (cols != null && cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b;
        }

        // Fallback: brug børns transforms
        var trs = root.GetComponentsInChildren<Transform>(true);
        if (trs != null && trs.Length > 1) // inkluderer root selv
        {
            Bounds b = new Bounds(trs[0].position, Vector3.zero);
            for (int i = 1; i < trs.Length; i++) b.Encapsulate(trs[i].position);
            return b;
        }

        return new Bounds(Vector3.zero, Vector3.zero);
    }
}
