using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class DocterCSVImporter : MonoBehaviour
{
    [Header("ドクターシートのCSV URL")]
    [SerializeField] private string csvUrl;

    [Header("ドクターのStatsData")]
    [SerializeField] private DocterStatsData docterStatsData;

    [ContextMenu("Import Docter CSV")]
    public void Import()
    {
        StartCoroutine(LoadCSV());
    }

    private IEnumerator LoadCSV()
    {
        using var req = UnityWebRequest.Get(csvUrl);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("ドクターCSV読み込み失敗: " + req.error);
            yield break;
        }

        ParseCSV(req.downloadHandler.text);
    }

    private void ParseCSV(string csv)
    {
        var lines = csv.Split('\n');
        var keys = new List<string>();
        var values = new List<string>();

        foreach (var line in lines)
        {
            var cols = line.Split(',');
            if (cols.Length < 2) continue;

            keys.Add(cols[0].Trim());
            values.Add(cols[1].Trim());
        }

        docterStatsData.SetData(keys, values);
        Debug.Log("ドクターステータスCSV読み込み完了");
    }
    [ContextMenu("Debug Docter Stats")]
    public void DebugStats()
    {
        Debug.Log("=== Docter Stats Debug ===");

        Debug.Log("資金: " + docterStatsData.GetInt("資金"));
        Debug.Log("チップ: " + docterStatsData.GetInt("チップ"));
        Debug.Log("スタミナ（体幹）: " + docterStatsData.GetInt("スタミナ（体幹）"));
        Debug.Log("突進: " + docterStatsData.GetInt("突進"));
        Debug.Log("防御: " + docterStatsData.GetInt("防御"));
        Debug.Log("スキル: " + docterStatsData.GetString("スキル"));
        Debug.Log("スキル内容: " +  docterStatsData.GetString("スキル内容"));

        Debug.Log("=== End ===");
    }
}
