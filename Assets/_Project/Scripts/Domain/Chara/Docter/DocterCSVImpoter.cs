using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class DocterCSVImporter : MonoBehaviour
{
    [Header("医者シートのCSV URL")]
    [SerializeField] private string csvUrl;

    [Header("医者のStatsData")]
    [SerializeField] private DocterStatsData doctorStatsData;

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

        doctorStatsData.SetData(keys, values);
        Debug.Log("医者ステータスCSV読み込み完了");
    }
    [ContextMenu("Debug Docter Stats")]
    public void DebugStats()
    {
        Debug.Log("=== Docter Stats Debug ===");

        Debug.Log("資金: " + doctorStatsData .GetInt("資金"));
        Debug.Log("チップ: " + doctorStatsData.GetInt("チップ"));
        Debug.Log("スタミナ（体幹）: " + doctorStatsData.GetInt("スタミナ（体幹）"));
        Debug.Log("突進: " + doctorStatsData.GetInt("突進"));
        Debug.Log("防御: " + doctorStatsData.GetInt("防御"));
        Debug.Log("スキル: " + doctorStatsData.GetString("スキル"));
        Debug.Log("スキル内容: " + doctorStatsData.GetString("スキル内容"));

        Debug.Log("=== End ===");
    }
}
