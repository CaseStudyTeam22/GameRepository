using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StatsData", menuName = "GameData/StatsData")]
public class StatsData : ScriptableObject
{
    [Header("キャラクター情報")]
    [Tooltip("キャラクター名")]
    public string characterName;

    [Tooltip("キャラクターの説明文")]
    public string description;

    [Tooltip("スキルの説明文")]
    public string skillDescription;

    [Header("ステータス一覧")]
    [Tooltip("資金、攻撃力、スタミナなどのステータスを保持する辞書")]
    public Dictionary<string, float> stats = new Dictionary<string, float>();

    /// <summary>
    /// 指定したキーのステータス値を取得する。
    /// キーが存在しない場合は 0 を返し、警告ログを出す。
    /// </summary>
    /// <param name="key">ステータス名（例: 'Attack', 'HP', 'Stamina'）</param>
    /// <returns>ステータス値。存在しない場合は 0。</returns>
    public float Get(string key)
    {
        if (stats.ContainsKey(key))
            return stats[key];

        Debug.LogWarning($"Key not found: {key}");
        return 0;
    }
}
