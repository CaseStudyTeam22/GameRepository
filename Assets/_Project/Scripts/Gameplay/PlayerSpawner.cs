using System.Collections.Generic;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	public class PlayerSpawner : MonoBehaviour
	{
		[SerializeField] PlayerView playerPrefab;
		[SerializeField] BoardView board;

		readonly Dictionary<string, PlayerView> _views = new();
		IGameState _state;

		public static PlayerSpawner Instance { get; private set; }
		public IReadOnlyDictionary<string, PlayerView> Views => _views;
		public PlayerView LocalPlayer =>
			_state != null && _state.MyId != null && _views.TryGetValue(_state.MyId, out var v) ? v : null;

		void Awake()
		{
			Instance = this;
		}

		void Start()
		{
			if (board == null) board = BoardView.Instance;

			_state = GameStateLocator.Current;
			if (_state == null)
			{
				Debug.LogError("[PlayerSpawner] GameStateLocator.Current is null. Make sure GameInstaller runs before PlayerSpawner (Awake before Start).");
				return;
			}
			_state.OnStateInitialized += SyncSpawns;
			_state.OnPlayersChanged   += SyncSpawns;
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
			if (_state == null) return;
			_state.OnStateInitialized -= SyncSpawns;
			_state.OnPlayersChanged   -= SyncSpawns;
		}

		void SyncSpawns()
		{
			foreach (var id in _state.Players.Keys)
				if (!_views.ContainsKey(id))
					Spawn(id);

			var stale = new List<string>();
			foreach (var id in _views.Keys)
				if (!_state.Players.ContainsKey(id))
					stale.Add(id);
			foreach (var id in stale)
				Despawn(id);
		}

		void Spawn(string id)
		{
			if (playerPrefab == null)
			{
				Debug.LogError("[PlayerSpawner] playerPrefab not assigned");
				return;
			}
			var view = Instantiate(playerPrefab, transform);
			view.name = $"Player_{id[..6]}";
			view.Bind(id, _state, board);
			_views[id] = view;
			Debug.Log($"[PlayerSpawner] spawned {id}");
		}

		void Despawn(string id)
		{
			if (!_views.TryGetValue(id, out var view)) return;
			_views.Remove(id);
			Destroy(view.gameObject);
			Debug.Log($"[PlayerSpawner] despawned {id}");
		}
	}
}
