using UnityEngine;

public class LevelChunk : MonoBehaviour
{
    public Transform entry;         // child named "Entry"
    public Transform exit;          // child named "Exit"
    public Collider2D spawnTrigger; // child named "SpawnTrigger" (IsTrigger)
    [HideInInspector] public bool spawnedNext;

    private void Reset()
    {
        entry = transform.Find("Entry");
        exit = transform.Find("Exit");
        spawnTrigger = transform.Find("SpawnTrigger")?.GetComponent<Collider2D>();
    }
    private void OnDrawGizmos()
    {
        if (entry)
        {
            Gizmos.DrawSphere(entry.position, 0.15f);
        }
        if (exit)
        {
            Gizmos.DrawCube(exit.position, Vector3.one * 0.25f);
        }
    }
}
