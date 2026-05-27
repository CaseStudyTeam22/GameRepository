using UnityEngine;
using GamblingAction.Core.Dto;

namespace GamblingAction.Domain
{
	/// <summary>
	/// 成金キャラクター。
	/// スプレッドシートから読み込んだ NarikinStatsData を使ってステータスを管理する。
	///
	/// 【キャラクター特性】
	///   - 初期所持金が他キャラより多い（20,000円）代わり、特殊な換金ルールを持つ
	///
	/// 【換金フェーズ（特殊ルール）】※ GameState 側の実装が必要
	///   - 換金フェーズが「存在しない」：代わりに相手が換金した分の「2倍」を自動取得する
	///   - 相手の換金チップより少なくなる場合は相手と同じ量になる
	///   - お互いが成金の場合は所持金の半分をかける
	///
	/// 【スキル（消費チップ: 0）】※ server.js 側の実装が必要
	///   - スキルを発動したターンに行動した場合、チップ消費量が 2 倍になり
	///     その分行動が強力になる
	///   - 消費したチップはフィールドにばらまかれる（相手・自分ともに拾える）
	/// </summary>
	public class NarikinChara : CharacterBase
	{
		// ─────────────────────────────────────────────────────────────
		// 定数：フォールバック値（スプレッドシートが読めなかった時に使う）
		// ─────────────────────────────────────────────────────────────

		/// <summary>スプレッドシート未読時の初期所持金</summary>
		private const int c_DefaultStartMoney = 20000;

		/// <summary>スプレッドシート未読時の初期チップ数</summary>
		private const int c_DefaultStartChips = 10;

		/// <summary>スプレッドシート未読時の最大スタミナ</summary>
		private const int c_DefaultMaxStamina = 10;

		/// <summary>スプレッドシート未読時の突進値</summary>
		private const int c_DefaultCharge = 4;

		/// <summary>スプレッドシート未読時の防御値</summary>
		private const int c_DefaultDefense = 1;

		// ─────────────────────────────────────────────────────────────
		// フィールド
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// スプレッドシートから読み込んだステータスデータ。
		/// Initialize() で注入する。
		/// ※ CharacterBase は MonoBehaviour ではないため Inspector からは設定できない
		/// </summary>
		private NarikinStatsData m_Stats;

		/// <summary>
		/// スキルがこのラウンドで発動済みかどうか。
		/// true の間、行動時のチップ消費が 2 倍になる（サーバー側実装待ち）。
		/// </summary>
		private bool m_IsSkillActive;

		// ─────────────────────────────────────────────────────────────
		// CharacterBase オーバーライド（ステータス）
		// ─────────────────────────────────────────────────────────────

		/// <summary>初期所持金。スプレッドシートの "資金" キーを参照する</summary>
		public override int StartMoney =>
			m_Stats != null ? m_Stats.GetInt("資金") : c_DefaultStartMoney;

		/// <summary>初期チップ数。スプレッドシートの "チップ" キーを参照する</summary>
		public override int StartChips =>
			m_Stats != null ? m_Stats.GetInt("チップ") : c_DefaultStartChips;

		/// <summary>最大スタミナ。スプレッドシートの "スタミナ（体幹）" キーを参照する</summary>
		public override int MaxStamina =>
			m_Stats != null ? m_Stats.GetInt("スタミナ（体幹）") : c_DefaultMaxStamina;

		/// <summary>突進値。スプレッドシートの "突進" キーを参照する</summary>
		public override int Charge =>
			m_Stats != null ? m_Stats.GetInt("突進") : c_DefaultCharge;

		/// <summary>防御値。スプレッドシートの "防御" キーを参照する</summary>
		public override int Defense =>
			m_Stats != null ? m_Stats.GetInt("防御") : c_DefaultDefense;

		/// <summary>キャラクター名。スプレッドシートの "キャラクター名" キーを参照する</summary>
		public override string CharacterName =>
			m_Stats != null ? m_Stats.GetString("キャラクター名") : "成金";

		// ─────────────────────────────────────────────────────────────
		// 初期化
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// ステータスデータを注入する。
		/// GameInstaller や NarikinConnector から呼ぶ。
		/// </summary>
		/// <param name="stats">NarikinStatsData アセット</param>
		public void Initialize(NarikinStatsData stats)
		{
			if (stats == null)
			{
				Debug.LogWarning($"[{CharacterName}] Initialize に null が渡された。フォールバック値を使用する。");
				return;
			}

			m_Stats = stats;
			Debug.Log($"[{CharacterName}] ステータスデータ読み込み完了。StartMoney={StartMoney}");
		}

		// ─────────────────────────────────────────────────────────────
		// スキル
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// スキル発動（消費チップ: 0）。
		/// 発動したターン中の行動でチップ消費を 2 倍にし、消費チップをフィールドにばらまく。
		///
		/// ※ 現状はローカルフラグのみ。チップ消費倍加とフィールドばらまきは
		///    server.js 側にフック処理の追加が必要。
		/// </summary>
		/// <param name="casterDto">スキルを使ったプレイヤーの DTO</param>
		public override void SkillEffect(PlayerDto casterDto)
		{
			PlayerId       = casterDto.Id;
			m_IsSkillActive = true;

			// TODO: サーバー側へスキル発動フラグを送信する処理を追加
			// 例: IGameState.SubmitSkillFlag(IntentTypes.Skill, "narikin_double_bet")
			Debug.Log($"[{CharacterName}] スキル発動！このターンの行動チップ消費が 2 倍になる。");
		}

		/// <summary>
		/// 遅延効果（次ラウンド開始時）。
		/// スキル発動フラグをリセットする。
		/// </summary>
		public override void DelayedEffect(IGameState state)
		{
			if (!m_IsSkillActive) return;

			m_IsSkillActive = false;
			Debug.Log($"[{CharacterName}] 遅延効果：スキルフラグをリセット。");
		}

		// ─────────────────────────────────────────────────────────────
		// 換金フェーズ特殊ルール（将来実装予定）
		// ─────────────────────────────────────────────────────────────

		// TODO: 換金フェーズのフックメソッド（CharacterBase / GameState に追加が必要）
		//
		// 仕様:
		//   ① 成金に換金フェーズは存在しない
		//   ② 相手が換金した枚数の 2 倍のチップを自動付与する
		//   ③ 相手の換金量より結果が少なくなるなら相手と同じ量にする
		//   ④ 双方が成金の場合は所持金の「半分」を担保として使う
		//
		// 実装例（GameState 側でフェーズ遷移時に呼び出す想定）:
		//
		// public void OnExchangePhase(PlayerDto selfDto, PlayerDto opponentDto)
		// {
		// 	// ④ 双方成金なら所持金の半分をチップに変換
		// 	if (opponentDto.CharacterType == "成金")
		// 	{
		// 		int halfMoney = selfDto.Money / 2;
		// 		selfDto.Chips += halfMoney / 100;  // 1チップ = 100円
		// 		selfDto.Money -= halfMoney;
		// 		Debug.Log($"[{CharacterName}] 双方成金ルール: 所持金の半分をかける");
		// 		return;
		// 	}
		//
		// 	// ② 相手の換金量の 2 倍
		// 	int narikinChips = opponentDto.ExchangedChips * 2;
		//
		// 	// ③ 相手より少ない場合は相手と同じ
		// 	if (narikinChips < opponentDto.ExchangedChips)
		// 		narikinChips = opponentDto.ExchangedChips;
		//
		// 	// 所持金が不足する場合はあるだけ使う
		// 	int cost       = narikinChips * 100;
		// 	int actualCost = Mathf.Min(cost, selfDto.Money);
		// 	selfDto.Chips += actualCost / 100;
		// 	selfDto.Money -= actualCost;
		//
		// 	Debug.Log($"[{CharacterName}] 自動換金: {actualCost / 100} チップ取得");
		// }
	}
}
