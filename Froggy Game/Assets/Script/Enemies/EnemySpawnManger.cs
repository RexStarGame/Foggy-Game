using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class EnemySpawnManger : MonoBehaviour
{
    [Header("What to spawn")]
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;

    [Header("Pooling")]
    public int preloadPerPrefab = 5;

    [Header("When to spawn")]
    public float firstSpawnDelay = 0f;      // delay before first tick
    public float spawnInterval = 60f;       // time between ticks
    public bool onlyOneAtATime = false;     // if true, batch is forced to 1

    [Header("Batch Spawning")]
    [Min(1)] public int batchSpawnCount = 2;           // how many to spawn per tick
    public bool requireDistinctPrefabs = true;         // all prefabs in a batch must differ
    public bool requireDistinctSpawnPoints = true;     // all spawn points in a batch must differ

    [Header("Auto-return defaults (applied if the prefab has no overrides)")]
    public float defaultLifeTime = 20f;           // 0 disables lifetime auto-return
    public bool defaultReturnWhenInvisible = true;

    [Header("Pooling Rules")]
    public bool allowInstantiateWhenPoolEmpty = true; // if false, skip spawn when pool empty
    public int hardCapPerPrefab = 50;                 // 0 = no cap

    [Header("Debug")]
    public bool debugLogging = false;
    public bool showHud = false;
    public float debugPrintInterval = 2f;

    private GameObject lastSpawned;

    // pools
    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();

    // debug counters
    private readonly Dictionary<GameObject, int> createdCount = new();   // per prefab
    private readonly Dictionary<GameObject, int> activeCount = new();   // per prefab
    private readonly Dictionary<GameObject, int> nextId = new();   // per prefab

    // one-time warnings
    private bool _warnedNotEnoughPrefabs = false;
    private bool _warnedNotEnoughPoints = false;
    [Header("Within-tick timing")]
    [Range(0f, 1f)] public float simultaneousChance = 0.5f; // % of ticks that spawn all instantly
    public float staggerDelayMin = 1f;                      // seconds
    public float staggerDelayMax = 5f;
    void Awake()
    {
        foreach (var prefab in enemyPrefabs)
        {
            if (!prefab) continue;

            if (!pools.ContainsKey(prefab)) pools[prefab] = new Queue<GameObject>();
            if (!createdCount.ContainsKey(prefab)) createdCount[prefab] = 0;
            if (!activeCount.ContainsKey(prefab)) activeCount[prefab] = 0;
            if (!nextId.ContainsKey(prefab)) nextId[prefab] = 1;

            for (int i = 0; i < preloadPerPrefab; i++)
            {
                var inst = Instantiate(prefab, transform);
                TagAndPrepareInstance(prefab, inst);
                var ret = inst.GetComponent<AutoReturnToSpawner>();
                if (ret) ret.enabled = false; // timer OFF while pooled
                inst.SetActive(false);
                pools[prefab].Enqueue(inst);
            }
        }

        if (showHud) StartCoroutine(DebugHudLoop());
    }

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        if (firstSpawnDelay > 0f)
            yield return new WaitForSeconds(firstSpawnDelay);

        while (true)
        {
            int countThisTick = onlyOneAtATime ? 1 : Mathf.Max(1, batchSpawnCount);
            // run an async routine that may stagger sub-spawns
            yield return StartCoroutine(SpawnBatchRoutine(countThisTick));
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    IEnumerator SpawnBatchRoutine(int count)
    {
        // Decide if this tick is simultaneous or staggered
        bool simultaneous = Random.value < simultaneousChance || count == 1;

        // Prepare distinct prefab & point selections using your existing helpers
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("[EnemySpawnManger] No enemyPrefabs assigned.");
            yield break;
        }

        // Choose prefabs
        var prefabIndices = GetRandomIndices(enemyPrefabs.Length, count, requireDistinctPrefabs);
        // Choose points
        Transform[] points = (spawnPoints != null && spawnPoints.Length > 0) ? spawnPoints : new Transform[] { transform };
        var pointIndices = GetRandomIndices(points.Length, count, requireDistinctSpawnPoints);

        int pairs = Mathf.Min(prefabIndices.Count, pointIndices.Count);
        if (pairs == 0) yield break;

        // If simultaneous: spawn all immediately
        if (simultaneous)
        {
            for (int i = 0; i < pairs; i++)
                SpawnOnePair(enemyPrefabs[prefabIndices[i]], points[pointIndices[i]]);
            yield break;
        }

        // Staggered: random delays (1–5s by default) between each sub-spawn
        for (int i = 0; i < pairs; i++)
        {
            // respect onlyOneAtATime: if last still alive, wait until it’s gone
            if (onlyOneAtATime && lastSpawned != null && lastSpawned.activeInHierarchy)
            {
                // poll until last is returned
                while (lastSpawned != null && lastSpawned.activeInHierarchy)
                    yield return null;
            }

            SpawnOnePair(enemyPrefabs[prefabIndices[i]], points[pointIndices[i]]);

            // delay before next sub-spawn, except after the last one
            if (i < pairs - 1)
                yield return new WaitForSeconds(Random.Range(staggerDelayMin, staggerDelayMax));
        }
    }
    void SpawnOnePair(GameObject prefab, Transform point)
    {
        var inst = GetFromPool(prefab);
        if (inst == null)
        {
            if (debugLogging) Debug.LogWarning($"[Pool] Spawn skipped for {prefab.name} (pool empty/capped).");
            return;
        }

        PrepareInstance(inst);
        var ret = inst.GetComponent<AutoReturnToSpawner>();
        if (ret)
        {
            ret.ownerSpawner = this;
            ret.ActivateForSpawn(); // reset + enable timer
        }

        inst.transform.SetPositionAndRotation(point.position, point.rotation);
        inst.SetActive(true);

        activeCount[prefab] = activeCount.TryGetValue(prefab, out var a) ? a + 1 : 1;
        lastSpawned = inst;
    }
    void SpawnBatch(int count)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("[EnemySpawnManger] No enemyPrefabs assigned.");
            return;
        }

        // --- choose prefabs (unique if required) ---
        var prefabIndices = GetRandomIndices(enemyPrefabs.Length, count, requireDistinctPrefabs);
        if (requireDistinctPrefabs && prefabIndices.Count < count && !_warnedNotEnoughPrefabs)
        {
            Debug.LogWarning("[EnemySpawnManger] Not enough distinct enemyPrefabs to satisfy batch; falling back.");
            _warnedNotEnoughPrefabs = true;
        }

        // --- choose spawn points (unique if required) ---
        Transform[] points = (spawnPoints != null && spawnPoints.Length > 0) ? spawnPoints : new Transform[] { transform };
        var pointIndices = GetRandomIndices(points.Length, count, requireDistinctSpawnPoints);
        if (requireDistinctSpawnPoints && pointIndices.Count < count && !_warnedNotEnoughPoints)
        {
            Debug.LogWarning("[EnemySpawnManger] Not enough distinct spawnPoints to satisfy batch; falling back.");
            _warnedNotEnoughPoints = true;
        }

        int pairs = Mathf.Min(prefabIndices.Count, pointIndices.Count);
        for (int i = 0; i < pairs; i++)
        {
            var prefab = enemyPrefabs[prefabIndices[i]];
            var point = points[pointIndices[i]];

            // if onlyOneAtATime, ensure last is gone
            if (onlyOneAtATime && lastSpawned != null && lastSpawned.activeInHierarchy)
                break;

            var inst = GetFromPool(prefab);
            if (inst == null)
            {
                if (debugLogging) Debug.LogWarning($"[Pool] Spawn skipped for {prefab.name} (pool empty/capped).");
                continue;
            }

            PrepareInstance(inst);
            var ret = inst.GetComponent<AutoReturnToSpawner>();
            if (ret)
            {
                ret.ownerSpawner = this;
                ret.ActivateForSpawn(); // reset + enable timer
            }

            inst.transform.SetPositionAndRotation(point.position, point.rotation);
            inst.SetActive(true);

            // debug: track active
            activeCount[prefab] = activeCount.TryGetValue(prefab, out var a) ? a + 1 : 1;

            lastSpawned = inst; // keep last for onlyOneAtATime compatibility
        }
    }

    // returns up to 'count' unique indices if requireUnique, otherwise allows repeats
    List<int> GetRandomIndices(int length, int count, bool requireUnique)
    {
        count = Mathf.Clamp(count, 1, Mathf.Max(1, length));
        if (!requireUnique || count == 1 || length == 1)
        {
            var list = new List<int>(count);
            for (int i = 0; i < count; i++) list.Add(Random.Range(0, length));
            return list;
        }

        // unique without replacement
        var all = Enumerable.Range(0, length).ToList();
        for (int i = 0; i < all.Count; i++)
        {
            int j = Random.Range(i, all.Count);
            (all[i], all[j]) = (all[j], all[i]);
        }
        return all.GetRange(0, Mathf.Min(count, length));
    }

    public GameObject SpawnOnce()  // kept for API compatibility; now spawns 1
    {
        SpawnBatch(1);
        return lastSpawned;
    }

    GameObject GetFromPool(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            pools[prefab] = q;
            if (!createdCount.ContainsKey(prefab)) createdCount[prefab] = 0;
            if (!activeCount.ContainsKey(prefab)) activeCount[prefab] = 0;
            if (!nextId.ContainsKey(prefab)) nextId[prefab] = 1;
        }

        // pop live instance
        while (q.Count > 0)
        {
            var inst = q.Dequeue();
            if (inst)
            {
                if (debugLogging) Debug.Log($"[Pool] Reused {inst.name}");
                if (!instanceToPrefab.ContainsKey(inst)) instanceToPrefab[inst] = prefab;
                return inst;
            }
        }

        // instantiate if allowed and under cap
        if (!allowInstantiateWhenPoolEmpty) return null;
        if (hardCapPerPrefab > 0 && createdCount.TryGetValue(prefab, out var created) && created >= hardCapPerPrefab)
        {
            if (debugLogging) Debug.LogWarning($"[Pool] HARD CAP reached for {prefab.name}, skipping instantiate.");
            return null;
        }

        var instNew = Instantiate(prefab, transform);
        TagAndPrepareInstance(prefab, instNew);
        if (debugLogging) Debug.Log($"[Pool] Instantiated NEW {instNew.name}");
        return instNew;
    }

    void TagAndPrepareInstance(GameObject prefab, GameObject inst)
    {
        int id = nextId[prefab];
        nextId[prefab] = id + 1;
        inst.name = $"{prefab.name}_ID{id}";

        createdCount[prefab] = createdCount.TryGetValue(prefab, out var c) ? c + 1 : 1;
        instanceToPrefab[inst] = prefab;

        PrepareInstance(inst);
    }

    void PrepareInstance(GameObject inst)
    {
        var ret = inst.GetComponent<AutoReturnToSpawner>();
        if (!ret) ret = inst.AddComponent<AutoReturnToSpawner>();
        ret.ownerSpawner = this;

        if (Mathf.Approximately(ret.lifeTime, 0f) && defaultLifeTime > 0f)
            ret.lifeTime = defaultLifeTime;
        if (!ret._explicitlySetReturnInvisible)
            ret.returnWhenInvisible = defaultReturnWhenInvisible;

        // ensure proxies on all child renderers
        var renderers = inst.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            var proxy = r.gameObject.GetComponent<RendererReturnProxy>();
            if (!proxy) proxy = r.gameObject.AddComponent<RendererReturnProxy>();
            proxy.target = ret;
        }
    }

    public void ReturnToPool(GameObject instance)
    {
        if (!instance) return;

        if (!instanceToPrefab.TryGetValue(instance, out var prefab) || !pools.ContainsKey(prefab))
        {
            instance.SetActive(false);
            return;
        }

        var ret = instance.GetComponent<AutoReturnToSpawner>();
        if (ret) ret.enabled = false; // stop timer while pooled

        instance.SetActive(false);
        pools[prefab].Enqueue(instance);

        activeCount[prefab] = Mathf.Max(0, activeCount[prefab] - 1);

        if (onlyOneAtATime && lastSpawned == instance)
            lastSpawned = null;
    }

    public void UnregisterInstance(GameObject instance)
    {
        instanceToPrefab.Remove(instance);
    }

    IEnumerator DebugHudLoop()
    {
        var sb = new StringBuilder();
        var wait = new WaitForSeconds(debugPrintInterval);

        while (true)
        {
            sb.Length = 0;
            sb.AppendLine("[POOL STATS]");
            foreach (var prefab in enemyPrefabs)
            {
                if (!prefab) continue;
                int created = createdCount.TryGetValue(prefab, out var c) ? c : 0;
                int active = activeCount.TryGetValue(prefab, out var a) ? a : 0;
                int pooled = pools.TryGetValue(prefab, out var q) ? q.Count : 0;
                sb.AppendLine($"{prefab.name} → Created:{created}  Active:{active}  InPool:{pooled}");
            }
            _lastHudText = sb.ToString();

            if (debugLogging) Debug.Log(_lastHudText);
            yield return wait;
        }
    }

    string _lastHudText = "";
    void OnGUI()
    {
        if (!showHud) return;
        GUI.Label(new Rect(10, 10, 520, 320), _lastHudText);
    }
}
