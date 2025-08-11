using UnityEngine;

public class BlueCarMovement : MonoBehaviour
{
    public float movementSpeed = 1f;
    private Rigidbody rb;

    public GameObject[] enemyPrefabs; // array af fjende-prefabs
    public Transform spawnPoint;      // punkt hvor fjender spawner

    public float respawnTime = 60f;   // 1 minut mellem spawns

    private GameObject currentEnemy;  // holder styr på den nuværende spawnede fjende
    private float timer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>(); // stavefejl rettet (GetCompoment -> GetComponent)
    }

    void Update()
    {
        // Bevæg bilen til højre
        transform.Translate(Vector3.right * movementSpeed * Time.deltaTime);

        // Hvis der ikke er en fjende i live
        if (currentEnemy == null)
        {
            timer += Time.deltaTime;
            if (timer >= respawnTime) // vent det ønskede antal sekunder
            {
                SpawnEnemy();
                timer = 0f;
            }
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefabs.Length == 0 || spawnPoint == null)
        {
            Debug.LogWarning("Ingen enemyPrefabs eller spawnPoint sat!");
            return;
        }

        int randomIndex = Random.Range(0, enemyPrefabs.Length);
        currentEnemy = Instantiate(
            enemyPrefabs[randomIndex],
            spawnPoint.position,
            Quaternion.identity
        );
    }
}
