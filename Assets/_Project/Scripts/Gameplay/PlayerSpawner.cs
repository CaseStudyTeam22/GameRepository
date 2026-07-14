using System.Collections.Generic;
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

		private readonly Dictionary<string, PlayerView> m_Views = new();
		private IGameState m_State;

		public static PlayerSpawner Instance { get; private set; }
		public IReadOnlyDictionary<string, PlayerView> Views => m_Views;
		// 一括生成で全員のキャラを出し終えたときに発火する。ラウンド開始の同期に使う。
		public event System.Action OnAllSpawned;
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
			// 退出したプレイヤーの despawn 同期のみ購読する。
			// 生成のタイミングは RoundFlow（RoundStartSequencer）が編成する。
			m_State.OnPlayersChanged += DespawnLeftPlayers;
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
			if (m_State == null) return;
			m_State.OnPlayersChanged -= DespawnLeftPlayers;
		}

		// 現在のプレイヤーのうち未生成のものを全員生成する。RoundFlow から呼ぶ。
		// 盤面の準備や待ち時間は RoundFlow 側で制御するため、ここでは即時生成する。
		public void SpawnAll()
		{
			if (m_State == null) m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[PlayerSpawner] SpawnAll: GameStateLocator.Current is null.");
				return;
			}
			if (m_Board == null) m_Board = BoardView.Instance;

			bool spawnedAny = false;
			foreach (var id in m_State.Players.Keys)
				if (!m_Views.ContainsKey(id))
				{
					Spawn(id);
					spawnedAny = true;
				}

			// 実際に生成し、かつ全員が揃ったときだけ通知する。
			if (spawnedAny && m_Views.Count >= m_State.Players.Count)
				OnAllSpawned?.Invoke();
		}

		// 全プレイヤーの View を破棄する。ラウンド間で作り直すために RoundFlow から呼ぶ。
		public void DespawnAll()
		{
			foreach (var view in m_Views.Values)
				if (view != null) Destroy(view.gameObject);
			m_Views.Clear();
		}

		// state から消えたプレイヤーの View を破棄する。
		private void DespawnLeftPlayers()
		{
			var stale = new List<string>();
			foreach (var id in m_Views.Keys)
				if (!m_State.Players.ContainsKey(id))
					stale.Add(id);
			foreach (var id in stale)
				Despawn(id);
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
