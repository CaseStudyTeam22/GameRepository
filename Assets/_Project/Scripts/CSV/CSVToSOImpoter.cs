using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class CSVToSOImporter : MonoBehaviour
{
    [SerializeField] private string csvUrl;      // スプレッドシートの CSV URL
    [SerializeField] private StatsData statsData;

    void Start()
    {
        StartCoroutine(LoadCSV());
    }

    IEnumerator LoadCSV()
    {
        UnityWebRequest req = UnityWebRequest.Get(csvUrl);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(req.error);
            yield break;
        }

        string csv = req.downloadHandler.text;
        ParseCSV(csv);

        Debug.Log($"Loaded: {statsData.characterName}");
        Debug.Log($"資金={statsData.Get("資金")}, チップ={statsData.Get("チップ")}, スタミナ={statsData.Get("スタミナ（体幹）")}, 攻撃力={statsData.Get("攻撃力")}, スキル={statsData.Get("スキル")}");
    }

    void ParseCSV(string csv)
    {
        statsData.stats.Clear();

        string[] lines = csv.Split('\n');

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split(',');

            string key = parts[0].Trim();

            // スキル内容は文字列
            if (key == "スキル内容")
            {
                statsData.skillDescription = parts[1].Trim();
                continue;
            }

            // 数値項目
            if (parts.Length > 1 && float.TryParse(parts[1], out float value))
            {
                statsData.stats[key] = value;
            }
        }

        // キャラ名・説明文は固定 or 後でスプシから取る
        statsData.characterName = "ドクター";
        statsData.description = "とある村で医者をしていたが一人の子供を救えず村から追い出されて気付いたらカジノにいた金を払えばどんな相手でも治療する";
    }
}
