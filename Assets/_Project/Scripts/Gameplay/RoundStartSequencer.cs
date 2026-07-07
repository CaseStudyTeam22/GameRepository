using System.Collections;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	// ラウンド開始の順序を編成する。各動作の開始秒数（本コンポーネント起動 = 0 秒）を持つ。
	// 場のクリアと再生成を別タイミングに分ける：
	//  - round_over（決着の演出開始）で即クリアし、画面を「何も無い」状態にする。
	//  - prepare_round（演出終了後）で盤面・キャラを時間軸に沿って作り直し、完了後に round_ready を返す。
	// 初回（Boot シーン読込直後）はクリア対象が無いだけで同じ再生成を通す。
	// 地形はラウンドごとに作り直す（毎回 ClearBoard → GenerateBoard）。
	// TODO: 表現が増えたら各動作を Track 化して Timeline 編成へ移行する。
	public class RoundStartSequencer : MonoBehaviour
	{
		[Header("時間軸（ラウンド開始 = 0 秒）")]
		[SerializeField, Tooltip("盤面生成を開始する秒数")]
		private float m_BoardGenerateTime = 0f;
		[SerializeField, Tooltip("キャラ生成を開始する秒数。盤面の登場演出が未完なら完了まで待つ")]
		private float m_CharaSpawnTime = 1.5f;

		// 依存先は単一インスタンスなので Inspector では持たず、Instance で取得する。
		private BoardView m_Board;
		private PlayerSpawner m_Spawner;
		private ItemSpawner m_Items;
		private IGameState m_State;

		private void Start()
		{
			m_Board = BoardView.Instance;
			m_Spawner = PlayerSpawner.Instance;
			m_Items = ItemSpawner.Instance;

			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[RoundStartSequencer] GameStateLocator.Current is null.");
				return;
			}
			if (m_Spawner == null)
			{
				Debug.LogError("[RoundStartSequencer] PlayerSpawner.Instance が見つかりません。");
				return;
			}

			// 決着の演出開始で即クリア、演出終了後の prepare_round で再生成する。
			m_State.OnRoundOver    += HandleRoundOver;
			m_State.OnPrepareRound += HandlePrepareRound;

			// 初回：クリア対象は無いが、同じ再生成を通す。
			RegenerateStage();
		}

		private void OnDestroy()
		{
			if (m_State != null)
			{
				m_State.OnRoundOver    -= HandleRoundOver;
				m_State.OnPrepareRound -= HandlePrepareRound;
			}
			if (m_Spawner != null) m_Spawner.OnAllSpawned -= HandleAllSpawned;
		}

		// 決着の演出開始。盤面・キャラ・item を破棄して「何も無い」状態に戻す。
		private void HandleRoundOver(string winnerRole)
		{
			ClearStage();
		}

		// 演出終了後。盤面・キャラを作り直す。
		private void HandlePrepareRound()
		{
			RegenerateStage();
		}

		private void ClearStage()
		{
			StopAllCoroutines();
			m_Board?.ClearBoard();
			m_Spawner.DespawnAll();
			m_Items?.ClearAll();
		}

		// 場をクリアしてから時間軸の生成を開始する。生成完了は OnAllSpawned で受ける。
		// （round_over でクリア済みでも、初回や取りこぼし対策として再度クリアしてから生成する。）
		private void RegenerateStage()
		{
			ClearStage();

			// 生成完了を 1 回だけ受け取る（二重購読しないよう一度外してから付ける）。
			m_Spawner.OnAllSpawned -= HandleAllSpawned;
			m_Spawner.OnAllSpawned += HandleAllSpawned;

			StartCoroutine(RunRoundTimeline());
		}

		// 時間軸：設定秒数で盤面生成 → キャラ生成、の順に起動する。
		private IEnumerator RunRoundTimeline()
		{
			// 盤面生成。
			if (m_BoardGenerateTime > 0f) yield return new WaitForSeconds(m_BoardGenerateTime);
			if (m_Board != null) m_Board.GenerateBoard();
			else Debug.LogError("[RoundStartSequencer] BoardView.Instance が見つかりません。");

			// キャラ生成。設定秒数を待ち、さらに盤面の登場演出が終わるまで待つ
			// （空の盤面に生成させないため、実触発 = max(設定秒数, 盤面ready)）。
			float remaining = m_CharaSpawnTime - m_BoardGenerateTime;
			if (remaining > 0f) yield return new WaitForSeconds(remaining);
			while (m_Board != null && !m_Board.IsBoardReady) yield return null;

			m_Spawner.SpawnAll();
		}

		private void HandleAllSpawned()
		{
			m_Spawner.OnAllSpawned -= HandleAllSpawned;
			SubmitReady();
		}

		private void SubmitReady()
		{
			Debug.Log("[RoundStartSequencer] round_ready を送信");
			m_State.SubmitRoundReady();
		}
	}
}
