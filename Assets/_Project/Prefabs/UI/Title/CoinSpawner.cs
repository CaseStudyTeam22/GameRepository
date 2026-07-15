using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    [SerializeField] GameObject coinPrefab;
    [SerializeField] float spawnInterval = 0.2f;
    [SerializeField] float spawnHeight = 10f;
    [SerializeField] float spawnRangeX = 5f;

    float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnCoin();
        }
    }

    void SpawnCoin()
    {
        float x = Random.Range(-spawnRangeX, spawnRangeX);
        Vector3 pos = new Vector3(x, spawnHeight, 0);

        // ƒ‰ƒ“ƒ_ƒ€‰ñ“]‚ÅŽ©‘R‚É—Ž‰º
        Quaternion rot = Quaternion.Euler(
            Random.Range(0, 360),
            Random.Range(0, 360),
            Random.Range(0, 360)
        );

        Instantiate(coinPrefab, pos, rot);
    }
}
