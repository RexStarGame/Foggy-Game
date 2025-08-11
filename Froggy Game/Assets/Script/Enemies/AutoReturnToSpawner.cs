using UnityEngine;

[DisallowMultipleComponent]
public class AutoReturnToSpawner : MonoBehaviour
{
    [Tooltip("Set by spawner on spawn. Optional to assign manually on prefab.")]
    public EnemySpawnManger ownerSpawner;

    [Header("Auto return rules (set 0 to disable lifetime)")]
    public float lifeTime = 0f;                 // counts only while active (enabled)
    public bool returnWhenInvisible = true;

    [HideInInspector] public bool _explicitlySetReturnInvisible = false;

    float t;
    bool returning = false; // guard against double-returns

    void Awake()
    {
        // CRITICAL: timer is OFF by default so preloaded/inactive objects never tick
        enabled = false;
        t = 0f;
        returning = false;
    }

    void OnValidate() { _explicitlySetReturnInvisible = true; }

    /// <summary>Called by the spawner right before SetActive(true).</summary>
    public void ActivateForSpawn()
    {
        t = 0f;
        returning = false;
        enabled = true;   // start ticking Update ONLY while live
    }

    void Update()
    {
        // if (!enabled) return;  // ← this line is redundant; safe to remove
        if (lifeTime > 0f)
        {
            t += Time.deltaTime;
            if (t >= lifeTime) DoReturn();
        }
    }

    void OnBecameInvisible()
    {
        // Only when actually active + timer enabled
        if (isActiveAndEnabled && returnWhenInvisible)
            DoReturn();
    }

    public void DoReturn()
    {
        if (returning) return;
        returning = true;

        if (ownerSpawner == null)
            ownerSpawner = GetComponentInParent<EnemySpawnManger>();

        // stop ticking while pooled
        enabled = false;

        if (ownerSpawner != null) ownerSpawner.ReturnToPool(gameObject);
        else gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // make sure we don't keep old time if disabled while live
        t = 0f;
        returning = false;
    }

    void OnDestroy()
    {
        // If someone Destroy()'d this by mistake, unregister so pool won't hold a dead ref
        if (ownerSpawner == null)
            ownerSpawner = GetComponentInParent<EnemySpawnManger>();
        ownerSpawner?.UnregisterInstance(gameObject);
    }
}
