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
			foreach (var id in m_State.Players.Keys)
				if (!m_Views.ContainsKey(id))
					Spawn(id);

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
