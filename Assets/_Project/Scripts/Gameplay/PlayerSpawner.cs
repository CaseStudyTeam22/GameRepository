using System.Collections.Generic;
using DG.Tweening;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	public class PlayerSpawner : MonoBehaviour
	{
		[FormerlySerializedAs("playerPrefab")]
		[SerializeField] private PlayerView m_PlayerPrefab;
		[FormerlySerializedAs("board")]
		[SerializeField] private BoardView m_Board;
		[Tooltip("盤面の登場演出が終わってからキャラを出すまでの待ち時間（秒）")]
		[SerializeField] private float m_SpawnDelayAfterBoard = 1.5f;

		private readonly Dictionary<string, PlayerView> m_Views = new();
		// 盤面待ちの一括生成フローを二重に仕掛けないためのフラグ。
		private bool m_BatchSpawnArmed;
		private IGameState m_State;

		public static PlayerSpawner Instance { get; private set; }
		public IReadOnlyDictionary<string, PlayerView> Views => m_Views;
		public PlayerView LocalPlayer =>
			m_State != null && m_State.MyId != null && m_Views.TryGetValue(m_State.MyId, out var v) ? v : null;

		private void Awake()
		{
			Instance = this;
		}

		private void Start()
		{
			if (m_Board == null) m_Board = BoardView.Instance;

			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[PlayerSpawner] GameStateLocator.Current is null. Make sure GameInstaller runs before PlayerSpawner (Awake before Start).");
				return;
			}
			m_State.OnStateInitialized += SyncSpawns;
			m_State.OnPlayersChanged   += SyncSpawns;
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
			if (m_State == null) return;
			m_State.OnStateInitialized -= SyncSpawns;
			m_State.OnPlayersChanged   -= SyncSpawns;
		}

		private void SyncSpawns()
		{
			if (m_Board == null) m_Board = BoardView.Instance;

			bool anyMissing = false;
			foreach (var id in m_State.Players.Keys)
				if (!m_Views.ContainsKey(id)) { anyMissing = true; break; }

			if (anyMissing)
			{
				// 盤面がまだなら登場演出 + 待ち時間のあと、全員を同時に生成する。
				if (m_Board != null && !m_Board.IsBoardReady)
					ArmBatchSpawn();
				else
					SpawnAllMissing();
			}

			var stale = new List<string>();
			foreach (var id in m_Views.Keys)
				if (!m_State.Players.ContainsKey(id))
					stale.Add(id);
			foreach (var id in stale)
				Despawn(id);
		}

		// 盤面完成 → 待ち時間 → 全員同時生成、の流れを一度だけ仕掛ける。
		private void ArmBatchSpawn()
		{
			if (m_BatchSpawnArmed) return;
			m_BatchSpawnArmed = true;
			m_Board.AddBoardReadyHandler(() =>
				DOVirtual.DelayedCall(m_SpawnDelayAfterBoard, () =>
				{
					if (this == null) return;
					SpawnAllMissing();
				}));
		}

		// 現在の全プレイヤーのうち未生成のものをまとめて生成する。
		private void SpawnAllMissing()
		{
			if (m_State == null) return;
			foreach (var id in m_State.Players.Keys)
				if (!m_Views.ContainsKey(id))
					Spawn(id);
		}

		private void Spawn(string id)
		{
			if (m_PlayerPrefab == null)
			{
				Debug.LogError("[PlayerSpawner] playerPrefab not assigned");
				return;
			}
			var view = Instantiate(m_PlayerPrefab, transform);
			view.name = $"Player_{id[..6]}";
			view.Bind(id, m_State, m_Board);
			m_Views[id] = view;
			Debug.Log($"[PlayerSpawner] spawned {id}");
		}

		private void Despawn(string id)
		{
			if (!m_Views.TryGetValue(id, out var view)) return;
			m_Views.Remove(id);
			Destroy(view.gameObject);
			Debug.Log($"[PlayerSpawner] despawned {id}");
		}
	}
}
