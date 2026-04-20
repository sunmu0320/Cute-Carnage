using UnityEngine;

public class SimpleZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;
    public Transform[] spawnPoints;
    public float spawnInterval = 5f;
    public int spawnCountPerWave = 2;
    public float timer;

    private int completedCycles;
    private bool warnedMissingPrefab;
    private bool warnedMissingSpawnPoints;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < spawnInterval)
        {
            return;
        }

        if (zombiePrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                Debug.LogWarning("[SimpleZombieSpawner] zombiePrefab is not assigned.", this);
                warnedMissingPrefab = true;
            }

            timer = 0f;
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            if (!warnedMissingSpawnPoints)
            {
                Debug.LogWarning("[SimpleZombieSpawner] spawnPoints is empty or not assigned.", this);
                warnedMissingSpawnPoints = true;
            }

            timer = 0f;
            return;
        }

        for (int i = 0; i < spawnCountPerWave; i++)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null)
            {
                continue;
            }

            Instantiate(zombiePrefab, point.position, point.rotation);
        }

        completedCycles++;
        if (completedCycles % 2 == 0)
        {
            spawnCountPerWave++;
        }

        timer = 0f;
    }
}
