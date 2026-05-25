using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	// 本轮開始の順序を制御する。盤面生成 → キャラ生成 の完了を待ってから
	// サーバへ round_ready を返し、兑换フェーズへ進ませる。
	// 初回（Boot シーン読込直後）は盤面・キャラを生成してから ready を返す。
	// 2 巡目以降（サーバの prepare_round）は盤面・キャラが残っているため即 ready を返す。
	public class RoundStartSequencer : MonoBehaviour
	{
		// 依存先は単一インスタンスなので Inspector では持たず、Instance で取得する。
		private BoardView m_Board;
		private PlayerSpawner m_Spawner;
		private IGameState m_State;

		private void Start()
		{
			m_Board = BoardView.Instance;
			m_Spawner = PlayerSpawner.Instance;

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

			// 2 巡目以降：盤面・キャラは場に残っているので即 ready を返す。
			m_State.OnPrepareRound += HandlePrepareRound;

			// 初回：既に全員生成済みなら（生成がこのコンポーネントより先に終わった場合）即 ready。
			if (m_State.Players.Count > 0 && m_Spawner.Views.Count >= m_State.Players.Count)
			{
				SubmitReady();
				return;
			}

			// 初回：キャラ生成の完了を待ってから ready を返す。
			m_Spawner.OnAllSpawned += HandleAllSpawned;

			// 先にキャラ生成を仕掛けてから盤面生成を起動する。
			// BeginRoundSpawn は OnBoardReady を購読するだけなので、盤面生成より先でよい。
			m_Spawner.BeginRoundSpawn();

			// 盤面生成を起動する（BoardView の自動生成は Boot シーンで無効化済み）。
			if (m_Board != null) m_Board.GenerateBoard();
			else Debug.LogError("[RoundStartSequencer] BoardView.Instance が見つかりません。");
		}

		private void OnDestroy()
		{
			if (m_State != null) m_State.OnPrepareRound -= HandlePrepareRound;
			if (m_Spawner != null) m_Spawner.OnAllSpawned -= HandleAllSpawned;
		}

		private void HandleAllSpawned()
		{
			m_Spawner.OnAllSpawned -= HandleAllSpawned;
			SubmitReady();
		}

		// 2 巡目以降。盤面・キャラはそのままなので即 ready を返す。
		private void HandlePrepareRound()
		{
			SubmitReady();
		}

		private void SubmitReady()
		{
			Debug.Log("[RoundStartSequencer] round_ready を送信");
			m_State.SubmitRoundReady();
		}
	}
}
