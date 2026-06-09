using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 成金キャラクターのステータスデータを保持する ScriptableObject。
/// Google スプレッドシートから取得した CSV の「キー・値ペア」を
/// 辞書として管理し、文字列／整数で取得できる。
///
/// 【スプレッドシートの列構成（想定）】
///   A列 = キー名（例: "資金"）
///   B列 = 値（例: "20000"）
///
/// 【作成方法】
///   Project ウィンドウで右クリック → Create → GameData → NarikinStatsData
/// </summary>
[CreateAssetMenu(fileName = "NarikinStatsData", menuName = "GameData/NarikinStatsData")]
public class NarikinStatsData : ScriptableObject
{
	// ─────────────────────────────────────────────────────────────
	// シリアライズフィールド
	// ─────────────────────────────────────────────────────────────

	/// <summary>CSV から読み込んだキー一覧（Inspector でも確認可能）</summary>
	[SerializeField] private List<string> m_Keys = new();

	/// <summary>CSV から読み込んだ値一覧（m_Keys と同インデックスで対応）</summary>
	[SerializeField] private List<string> m_Values = new();

	// ─────────────────────────────────────────────────────────────
	// 内部状態
	// ─────────────────────────────────────────────────────────────

	/// <summary>高速検索用の辞書（シリアライズ非対象）</summary>
	private Dictionary<string, string> m_Dict;

	// ─────────────────────────────────────────────────────────────
	// ライフサイクル
	// ─────────────────────────────────────────────────────────────

	/// <summary>アセット読み込み時にリストから辞書を構築する</summary>
	private void OnEnable()
	{
		BuildDict();
	}

	// ─────────────────────────────────────────────────────────────
	// パブリック API
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// キーに対応する int 値を返す。
	/// キーが存在しない、もしくは数値変換に失敗した場合は 0 を返す。
	/// </summary>
	public int GetInt(string key)
	{
		EnsureDict();
		return m_Dict.TryGetValue(key, out var v) && int.TryParse(v, out var result)
			? result
			: 0;
	}

	/// <summary>
	/// キーに対応する string 値を返す。
	/// キーが存在しない場合は空文字を返す。
	/// </summary>
	public string GetString(string key)
	{
		EnsureDict();
		return m_Dict.TryGetValue(key, out var v) ? v : string.Empty;
	}

	/// <summary>
	/// CSV インポーターからデータを受け取り、辞書を再構築する。
	/// </summary>
	/// <param name="newKeys">CSV の A 列（キー名リスト）</param>
	/// <param name="newValues">CSV の B 列（値リスト）</param>
	public void SetData(List<string> newKeys, List<string> newValues)
	{
		m_Keys   = newKeys;
		m_Values = newValues;
		BuildDict();

#if UNITY_EDITOR
		// エディタ上でのみアセットへの変更をダーティにして保存候補にする
		UnityEditor.EditorUtility.SetDirty(this);
#endif
	}

	// ─────────────────────────────────────────────────────────────
	// 内部処理
	// ─────────────────────────────────────────────────────────────

	/// <summary>辞書が未初期化の場合に構築する（遅延初期化）</summary>
	private void EnsureDict()
	{
		if (m_Dict == null) BuildDict();
	}

	/// <summary>m_Keys / m_Values のリストから m_Dict を再構築する</summary>
	private void BuildDict()
	{
		m_Dict = new Dictionary<string, string>();

		int count = Mathf.Min(m_Keys.Count, m_Values.Count);
		for (int i = 0; i < count; i++)
		{
			// キーが空の行は無視する
			if (string.IsNullOrEmpty(m_Keys[i])) continue;

			// 重複キーは後勝ち（スプレッドシート側で管理すること）
			m_Dict[m_Keys[i]] = m_Values[i];
		}
	}
}
