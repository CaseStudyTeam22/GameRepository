using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class FighterCSVImporter : MonoBehaviour
{
    [Header("格闘家シートのCSV URL")]
    [SerializeField] private string csvUrl;

    [Header("格闘家のStatsData")]
    [SerializeField] private FighterStatsData fighterStatsData;

    [ContextMenu("Import Fighter CSV")]
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
            Debug.LogError("格闘家CSV読み込み失敗: " + req.error);
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

        fighterStatsData.SetData(keys, values);
        Debug.Log("格闘家ステータスCSV読み込み完了");
    }
    [ContextMenu("Debug Fighter Stats")]
    public void DebugStats()
    {
        Debug.Log("=== Fighter Stats Debug ===");

        Debug.Log("資金: " + fighterStatsData.GetInt("資金"));
        Debug.Log("チップ: " + fighterStatsData.GetInt("チップ"));
        Debug.Log("スタミナ（体幹）: " + fighterStatsData.GetInt("スタミナ（体幹）"));
        Debug.Log("突進: " + fighterStatsData.GetInt("突進"));
        Debug.Log("防御: " + fighterStatsData.GetInt("防御"));
        Debug.Log("スキル: " + fighterStatsData.GetString("スキル"));
        Debug.Log("スキル内容: " + fighterStatsData.GetString("スキル内容"));

        Debug.Log("=== End ===");
    }
}
