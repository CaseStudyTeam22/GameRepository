using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Google スプレッドシートの公開 CSV から成金のステータスを読み込むインポーター。
///
/// 【セットアップ手順】
///   1. Google スプレッドシートを開く
///   2. ファイル → 共有 → 「ウェブに公開」
///   3. 形式を「カンマ区切りの値（.csv）」に設定して公開
///   4. 取得した URL を Inspector の「成金シートの CSV URL」に貼り付ける
///   5. Inspector の「成金の StatsData」に NarikinStatsData アセットを割り当てる
///   6. ContextMenu の「Import Narikin CSV」を実行すると読み込まれる
///
/// 【CSV の列構成（スプレッドシート側で合わせること）】
///   A列 : キー名（例: "資金"、"チップ"）
///   B列 : 値（例: "20000"、"10"）
/// </summary>
public class NarikinCSVImporter : MonoBehaviour
{
	// ─────────────────────────────────────────────────────────────
	// シリアライズフィールド
	// ─────────────────────────────────────────────────────────────

	[Header("成金シートのCSV URL")]
	[SerializeField] private string m_CsvUrl;

	[Header("成金のStatsData")]
	[SerializeField] private NarikinStatsData m_NarikinStatsData;

	// ─────────────────────────────────────────────────────────────
	// パブリック API
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// CSV を非同期取得してステータスデータに反映する。
	/// Inspector の ContextMenu または外部コードから呼び出す。
	/// </summary>
	[ContextMenu("Import Narikin CSV")]
	public void Import()
	{
		if (string.IsNullOrEmpty(m_CsvUrl))
		{
			Debug.LogError("[NarikinCSVImporter] CSV URL が設定されていません。");
			return;
		}

		if (m_NarikinStatsData == null)
		{
			Debug.LogError("[NarikinCSVImporter] NarikinStatsData が設定されていません。");
			return;
		}

		StartCoroutine(LoadCSV());
	}

	/// <summary>現在のステータスをデバッグログに一覧出力する</summary>
	[ContextMenu("Debug Narikin Stats")]
	public void DebugStats()
	{
		if (m_NarikinStatsData == null)
		{
			Debug.LogError("[NarikinCSVImporter] NarikinStatsData が設定されていません。");
			return;
		}

		Debug.Log("=== Narikin Stats Debug ===");
		Debug.Log("キャラクター名    : " + m_NarikinStatsData.GetString("キャラクター名"));
		Debug.Log("資金              : " + m_NarikinStatsData.GetInt("資金"));
		Debug.Log("チップ            : " + m_NarikinStatsData.GetInt("チップ"));
		Debug.Log("スタミナ（体幹）  : " + m_NarikinStatsData.GetInt("スタミナ（体幹）"));
		Debug.Log("突進              : " + m_NarikinStatsData.GetInt("突進"));
		Debug.Log("突進 消費チップ   : " + m_NarikinStatsData.GetInt("突進消費チップ"));
		Debug.Log("防御              : " + m_NarikinStatsData.GetInt("防御"));
		Debug.Log("防御 消費チップ   : " + m_NarikinStatsData.GetInt("防御消費チップ"));
		Debug.Log("スキル            : " + m_NarikinStatsData.GetInt("スキル"));
		Debug.Log("スキル内容        : " + m_NarikinStatsData.GetString("スキル内容"));
		Debug.Log("=== End ===");
	}

	// ─────────────────────────────────────────────────────────────
	// 内部処理
	// ─────────────────────────────────────────────────────────────

	/// <summary>UnityWebRequest を使って URL から CSV テキストを非同期取得する</summary>
	private IEnumerator LoadCSV()
	{
		Debug.Log("[NarikinCSVImporter] CSV 読み込み開始...");

		using var req = UnityWebRequest.Get(m_CsvUrl);
		yield return req.SendWebRequest();

		if (req.result != UnityWebRequest.Result.Success)
		{
			Debug.LogError("[NarikinCSVImporter] CSV 読み込み失敗: " + req.error);
			yield break;
		}

		ParseCSV(req.downloadHandler.text);
		Debug.Log("[NarikinCSVImporter] 成金ステータス CSV 読み込み完了。");
	}

	/// <summary>
	/// CSV テキストを行ごとにパースし、キーと値のリストを作成して
	/// NarikinStatsData に渡す。
	///
	/// 対応形式 :
	///   - 1 行目がヘッダーでも値でも問わない（キーが空でなければ取り込む）
	///   - A 列がキー、B 列が値（C 列以降は無視）
	///   - 空行はスキップ
	/// </summary>
	private void ParseCSV(string csv)
	{
		var lines  = csv.Split('\n');
		var keys   = new List<string>();
		var values = new List<string>();

		foreach (var line in lines)
		{
			// 空行はスキップ
			if (string.IsNullOrWhiteSpace(line)) continue;

			var cols = line.Split(',');

			// 列が 2 つ未満の行はスキップ
			if (cols.Length < 2) continue;

			string key   = cols[0].Trim();
			string value = cols[1].Trim();

			// キーが空の行はスキップ
			if (string.IsNullOrEmpty(key)) continue;

			keys.Add(key);
			values.Add(value);
		}

		m_NarikinStatsData.SetData(keys, values);
	}
}
