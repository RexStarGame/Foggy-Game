using UnityEngine;

public class LevelController : MonoBehaviour
{
    public LevelGenerator levelGenerator;

    private void Awake()
    {
        if (!levelGenerator)
            levelGenerator = Object.FindFirstObjectByType<LevelGenerator>();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            levelGenerator.SpawnNewLevel(transform); // now resolves
    }
}
