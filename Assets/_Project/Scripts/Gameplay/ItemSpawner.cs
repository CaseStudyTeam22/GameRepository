using System.Collections.Generic;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	public class ItemSpawner : MonoBehaviour
	{
		[SerializeField] ItemView itemPrefab;

		readonly Dictionary<double, ItemView> _views = new();
		IGameState _state;
		IBoardCoords _board;

		void Start()
		{
			_state = GameStateLocator.Current;
			_board = BoardCoordsLocator.Current;
			if (_state == null || _board == null)
			{
				Debug.LogError("[ItemSpawner] Locator not ready");
				return;
			}
			_state.OnItemsChanged += SyncItems;
		}

		void OnDestroy()
		{
			if (_state != null) _state.OnItemsChanged -= SyncItems;
		}

		void SyncItems()
		{
			var current = new HashSet<double>();
			foreach (var dto in _state.Items)
			{
				current.Add(dto.Id);
				if (_views.ContainsKey(dto.Id)) continue;
				if (itemPrefab == null)
				{
					Debug.LogError("[ItemSpawner] itemPrefab not assigned");
					return;
				}
				var view = Instantiate(itemPrefab, transform);
				view.name = $"Item_{dto.Type}_{dto.Id:F0}";
				view.Bind(dto, _board);
				_views[dto.Id] = view;
			}

			var stale = new List<double>();
			foreach (var id in _views.Keys)
				if (!current.Contains(id)) stale.Add(id);
			foreach (var id in stale)
			{
				Destroy(_views[id].gameObject);
				_views.Remove(id);
			}
		}
	}
}
