using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [Header("Setup")]
    public GameObject[] chunkPrefabs;
    public Transform player;

    [Header("Placement")]
    public float extraGapY = 0f;        // tweak how far “ahead” the next chunk spawns
    public bool alternateFlip = false;  // optional mirroring variety

    [Header("Lifetime")]
    public int keepChunks = 6;              // how many chunks stay alive
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

        var first = Instantiate(GetRandomPrefab(), Vector3.zero, Quaternion.identity)
                    .GetComponent<LevelChunk>();
        live.Enqueue(first);
    }

    // New API
    public void SpawnNext(LevelChunk from)
    {
        if (quitting || from == null) return;

        var next = Instantiate(GetRandomPrefab()).GetComponent<LevelChunk>();

        // Align next.entry to from.exit + extraGapY
        Vector3 target = from.exit.position + Vector3.up * extraGapY;
        Vector3 delta = target - next.entry.position;
        next.transform.position += delta;

        if (alternateFlip && flipNext) MirrorX(next.transform);
        flipNext = alternateFlip ? !flipNext : flipNext;

        // Keep memory tidy
        live.Enqueue(next);
        while (live.Count > keepChunks)
        {
            var old = live.Dequeue();
            if (old) Destroy(old.gameObject);
        }
    }

    // Back-compat so your old LevelController still works
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

    private GameObject GetRandomPrefab() => chunkPrefabs[Random.Range(0, chunkPrefabs.Length)];

    private void Update()
    {
        // Optional auto-despawn behind player
        if (player && live.Count > 0)
        {
            var oldest = live.Peek();
            if (oldest && player.position.y - oldest.exit.position.y > despawnBehindDistance)
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
}
