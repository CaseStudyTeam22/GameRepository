using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private StatsData stats;

    void Start()
    {
        float money = stats.Get("資金");
        float chip = stats.Get("チップ");
        float stamina = stats.Get("スタミナ（体幹）");
        float attack = stats.Get("攻撃力");
        float skill = stats.Get("スキル");

        Debug.Log($"資金={money}, チップ={chip}, スタミナ={stamina}, 攻撃力={attack}, スキル={skill}");
        Debug.Log($"スキル内容={stats.skillDescription}");
    }
}
