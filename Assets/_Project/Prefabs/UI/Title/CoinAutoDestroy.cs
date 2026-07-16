using UnityEngine;

public class CoinAutoDestroy : MonoBehaviour
{
    [SerializeField] float lifeTime = 3f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}
