using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ChunkSpawnTrigger : MonoBehaviour
{
    [SerializeField] private LevelChunk owner;
    [SerializeField] private LevelGenerator generator;

    private void Awake()
    {
        if (!owner) owner = GetComponentInParent<LevelChunk>();
        if (!generator) generator = Object.FindFirstObjectByType<LevelGenerator>(); // new API
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!owner || !generator) return;
        if (!other.CompareTag("Player")) return;
        if (owner.spawnedNext) return;           // guard

        owner.spawnedNext = true;
        GetComponent<Collider2D>().enabled = false; // slå fra, så den ikke kan trigges igen
        generator.SpawnNext(owner);
    }
}
