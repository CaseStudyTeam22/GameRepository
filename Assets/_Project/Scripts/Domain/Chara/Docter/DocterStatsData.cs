using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DocterStatsData", menuName = "GameData/DocterStatsData")]
public class DocterStatsData : ScriptableObject
{
    [SerializeField] private List<string> keys = new();
    [SerializeField] private List<string> values = new();

    private Dictionary<string, string> dict;

    private void OnEnable()
    {
        dict = new Dictionary<string, string>();
        for (int i = 0; i < keys.Count; i++)
        {
            dict[keys[i]] = values[i];
        }
    }

    public int GetInt(string key)
    {
        return dict.TryGetValue(key, out var v) && int.TryParse(v, out var result)
            ? result
            : 0;
    }

    public string GetString(string key)
    {
        return dict.TryGetValue(key, out var v) ? v : "";
    }

    public void SetData(List<string> newKeys, List<string> newValues)
    {
        keys = newKeys;
        values = newValues;
        OnEnable();
    }
}
