using System.Collections;
using GamblingAction.Core;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// コントローラーの振動（ハプティクス）を一元管理するコントローラー。
	///
	/// 【担当する振動】
	///   ① ビート連動  : 1・2・3拍は軽い脈動、4拍目（大拍子）は強い振動（Hi-Fi Rush 風）
	///   ② 突進        : 自分が突進したとき / 突進されたとき（被push）に強振動
	///   ③ 移動        : 移動距離に応じて強さを変える（1マス=弱 / 2マス=中 / 3マス=強）
	///
	/// 【設計方針】
	///   - InputModule は「入力 → サーバー送信」の役割なので、出力である振動は本クラスに分離する。
	///   - 結果イベント（突進・移動・衝突）はすべて IGameState.OnGameEvents 経由で受け取る。
	///     自分が対象（TargetId == MyId）のイベントだけを拾って振動させる。
	///   - Input System の SetMotorSpeeds は自動で止まらないため、
	///     コルーチンで一元的に時間管理し、終了時に必ず 0 へ戻す。
	///   - 振動が重なった場合は「強い方を優先」して上書きし、モーターの取り合いを防ぐ。
	///
	/// 【デメリット・制約】
	///   - 振動対応は Gamepad のみ。キーボード/マウス操作時は何も起きない（仕様）。
	///   - DualSense 等のトリガーハプティクスではなく、左右モーターの汎用振動のみ対応。
	///   - 振動の ON/OFF や強さはインスペクターで調整可能。苦手なプレイヤー向けに
	///     m_HapticsEnabled で全体を切れるようにしてある（オプション化の足がかり）。
	/// </summary>
	public class HapticsController : MonoBehaviour
	{
		// ─────────────────────────────────────────────────────────────
		// 全体設定
		// ─────────────────────────────────────────────────────────────

		[Header("全体")]
		[SerializeField, Tooltip("振動全体の ON/OFF。OFF にすると一切振動しない")]
		private bool m_HapticsEnabled = true;

		[SerializeField, Range(0f, 1f), Tooltip("全振動に掛かるマスター強度。1で設計値どおり、下げると全体が弱くなる")]
		private float m_MasterScale = 1f;

		// ─────────────────────────────────────────────────────────────
		// ① ビート連動振動
		// ─────────────────────────────────────────────────────────────

		[Header("ビート連動")]
		[SerializeField, Tooltip("ビート連動振動を使うか")]
		private bool m_BeatHapticsEnabled = true;

		[SerializeField, Range(0f, 1f), Tooltip("通常拍（1・2・3）の低周波モーター強度")]
		private float m_NormalBeatLow = 0.15f;

		[SerializeField, Range(0f, 1f), Tooltip("通常拍（1・2・3）の高周波モーター強度")]
		private float m_NormalBeatHigh = 0.05f;

		[SerializeField, Tooltip("通常拍の振動時間（秒）。短く弾むように")]
		private float m_NormalBeatDuration = 0.06f;

		[SerializeField, Range(0f, 1f), Tooltip("大拍子（4拍目）の低周波モーター強度")]
		private float m_BigBeatLow = 0.7f;

		[SerializeField, Range(0f, 1f), Tooltip("大拍子（4拍目）の高周波モーター強度")]
		private float m_BigBeatHigh = 0.55f;

		[SerializeField, Tooltip("大拍子の振動時間（秒）。ドンと長めに")]
		private float m_BigBeatDuration = 0.18f;

		// ─────────────────────────────────────────────────────────────
		// ② 突進振動
		// ─────────────────────────────────────────────────────────────

		[Header("突進（する / される）")]
		[SerializeField, Range(0f, 1f), Tooltip("突進アクションの低周波モーター強度")]
		private float m_PushLow = 0.85f;

		[SerializeField, Range(0f, 1f), Tooltip("突進アクションの高周波モーター強度")]
		private float m_PushHigh = 0.6f;

		[SerializeField, Tooltip("突進の振動時間（秒）")]
		private float m_PushDuration = 0.22f;

		[SerializeField, Range(0f, 1f), Tooltip("突き飛ばされた（被push）ときの低周波モーター強度")]
		private float m_PushedLow = 1.0f;

		[SerializeField, Range(0f, 1f), Tooltip("突き飛ばされた（被push）ときの高周波モーター強度")]
		private float m_PushedHigh = 0.8f;

		[SerializeField, Tooltip("被pushの振動時間（秒）。一番強く長く")]
		private float m_PushedDuration = 0.3f;

		// ─────────────────────────────────────────────────────────────
		// ③ 移動振動（距離別）
		// ─────────────────────────────────────────────────────────────

		[Header("移動（距離別）")]
		[SerializeField, Range(0f, 1f), Tooltip("1マス移動（弱）の低周波モーター強度")]
		private float m_Move1Low = 0.2f;

		[SerializeField, Range(0f, 1f), Tooltip("2マス移動（中）の低周波モーター強度")]
		private float m_Move2Low = 0.45f;

		[SerializeField, Range(0f, 1f), Tooltip("3マス以上移動（強）の低周波モーター強度")]
		private float m_Move3Low = 0.7f;

		[SerializeField, Tooltip("移動振動の高周波モーター強度（距離共通）")]
		private float m_MoveHigh = 0.15f;

		[SerializeField, Tooltip("移動振動の時間（秒）")]
		private float m_MoveDuration = 0.12f;

		// ─────────────────────────────────────────────────────────────
		// 内部状態
		// ─────────────────────────────────────────────────────────────

		private IGameState m_State;

		// 現在の振動を時間管理するコルーチン（1本だけ走らせて上書き方式にする）
		private Coroutine m_RumbleRoutine;

		// 現在鳴っている振動の低周波強度。これより弱い振動が来たら無視する（強い方優先）
		private float m_CurrentLow;

		// 現在振動中のパッド（停止時に 0 を書き込むため、鳴らした相手を覚えておく）。
		// 常時保持はせず、Rumble のたびに Gamepad.current から取り直す。
		private Gamepad m_ActivePad;

		// ─────────────────────────────────────────────────────────────
		// ライフサイクル
		// ─────────────────────────────────────────────────────────────

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[Haptics] GameStateLocator.Current is null");
				return;
			}

			m_State.OnBeatChanged += HandleBeatChanged;
			m_State.OnGameEvents  += HandleGameEvents;

			// 【重要】ここでは Gamepad.current / Gamepad.all を触らない。
			// 起動時にデバイスへ能動アクセスすると Multiplayer Play Mode の
			// インスタンス別デバイス分離が壊れ、両インスタンスが同じパッド入力を
			// 受け取ってしまう（両キャラが同じ動きをする）。
			// そのため、パッドは「振動を鳴らす瞬間」にだけ都度参照する方式にする。
		}

		private void OnDestroy()
		{
			if (m_State != null)
			{
				m_State.OnBeatChanged -= HandleBeatChanged;
				m_State.OnGameEvents  -= HandleGameEvents;
			}

			// 念のため振動を止めてから破棄する（鳴りっぱなし防止）
			StopRumbleImmediate();
		}

		private void OnDisable()
		{
			// 非アクティブ化されたときも確実に止める
			StopRumbleImmediate();
		}

		// アプリがバックグラウンドに回ったときに振動が固まるのを防ぐ
		private void OnApplicationFocus(bool hasFocus)
		{
			if (!hasFocus) StopRumbleImmediate();
		}

		// ─────────────────────────────────────────────────────────────
		// ① ビート連動
		// ─────────────────────────────────────────────────────────────

		private void HandleBeatChanged()
		{
			if (!m_BeatHapticsEnabled) return;
			if (m_State == null) return;

			int beat = m_State.CurrentBeat;

			// 大拍子（最終拍）かどうかで強さを切り替える
			if (beat >= GameConfig.BeatsPerCycle)
				Rumble(m_BigBeatLow, m_BigBeatHigh, m_BigBeatDuration);
			else
				Rumble(m_NormalBeatLow, m_NormalBeatHigh, m_NormalBeatDuration);
		}

		// ─────────────────────────────────────────────────────────────
		// ②③ 結果イベント（突進・移動・衝突）
		// ─────────────────────────────────────────────────────────────

		private void HandleGameEvents(EventDto[] events)
		{
			if (m_State == null) return;
			if (events == null) return;

			string myId = m_State.MyId;

			foreach (var ev in events)
			{
				if (ev == null) continue;

				switch (ev.Type)
				{
					// ── 突き飛ばされた（被push）：自分が対象なら強振動 ──
					case EventTypes.Pushed:
						if (ev.TargetId == myId)
							Rumble(m_PushedLow, m_PushedHigh, m_PushedDuration);
						break;

					// ── 攻撃/突進がヒット：移動距離に応じて振動 ──
					// Hit には dist（移動・押し出し距離）が乗ってくる。
					// 自分が突進した側 / 当てられた側のどちらでも体感が欲しいので
					// TargetId == myId（自分が当たった）に加え、
					// players に自分が含まれる（自分が起こした）場合も拾う。
					case EventTypes.Hit:
						if (IsAboutMe(ev, myId))
							RumbleByDistance(ev.Dist);
						break;
				}
			}
		}

		// イベントが自分に関係するか判定する。
		// TargetId が自分、または players 配列に自分が含まれていれば true。
		private bool IsAboutMe(EventDto ev, string myId)
		{
			if (ev.TargetId == myId) return true;
			if (ev.Players != null)
			{
				foreach (var p in ev.Players)
					if (p == myId) return true;
			}
			return false;
		}

		// 移動距離（マス数）に応じて弱・中・強を出し分ける。
		// dist が null（距離情報なし）の場合は突進相当の強振動を鳴らす。
		private void RumbleByDistance(int? dist)
		{
			if (!dist.HasValue)
			{
				// 距離不明＝突進アクション扱いで強めに鳴らす
				Rumble(m_PushLow, m_PushHigh, m_PushDuration);
				return;
			}

			int d = dist.Value;
			float low;
			if (d <= 1)      low = m_Move1Low; // 1マス：弱
			else if (d == 2) low = m_Move2Low; // 2マス：中
			else             low = m_Move3Low; // 3マス以上：強

			Rumble(low, m_MoveHigh, m_MoveDuration);
		}

		// ─────────────────────────────────────────────────────────────
		// 振動の実行（共通）
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// 指定強度・時間で振動させる。
		/// 既に鳴っている振動より弱い場合は無視する（強い演出が途中で薄まらないように）。
		/// </summary>
		/// <param name="low">低周波モーター強度（0〜1）。ズシッとした重い揺れ</param>
		/// <param name="high">高周波モーター強度（0〜1）。細かい振動</param>
		/// <param name="duration">振動時間（秒）</param>
		private void Rumble(float low, float high, float duration)
		{
			if (!m_HapticsEnabled) return;

			// 鳴らす瞬間にだけパッドを参照する（常時保持はしない）。
			// これにより起動時のデバイス分離を壊さず、MPPM でも安全。
			var pad = Gamepad.current;
			if (pad == null) return; // コントローラー未接続なら何もしない

			// マスター強度を掛ける
			low  = Mathf.Clamp01(low  * m_MasterScale);
			high = Mathf.Clamp01(high * m_MasterScale);

			// 既存の振動より弱いなら上書きしない（強い方優先）
			if (m_RumbleRoutine != null && low < m_CurrentLow) return;

			if (m_RumbleRoutine != null) StopCoroutine(m_RumbleRoutine);
			m_CurrentLow   = low;
			m_ActivePad    = pad;
			m_RumbleRoutine = StartCoroutine(RumbleRoutine(pad, low, high, duration));
		}

		private IEnumerator RumbleRoutine(Gamepad pad, float low, float high, float duration)
		{
			pad.SetMotorSpeeds(low, high);
			yield return new WaitForSeconds(duration);
			pad.SetMotorSpeeds(0f, 0f);
			m_CurrentLow    = 0f;
			m_ActivePad     = null;
			m_RumbleRoutine = null;
		}

		// 進行中の振動を即座に止める（モーターを 0 に戻す）。
		private void StopRumbleImmediate()
		{
			if (m_RumbleRoutine != null)
			{
				StopCoroutine(m_RumbleRoutine);
				m_RumbleRoutine = null;
			}
			m_CurrentLow = 0f;

			// 鳴らしていたパッドがあれば止める。なければ current にも念のため 0 を送る。
			if (m_ActivePad != null)
				m_ActivePad.SetMotorSpeeds(0f, 0f);
			else if (Gamepad.current != null)
				Gamepad.current.SetMotorSpeeds(0f, 0f);

			m_ActivePad = null;
		}
	}
}