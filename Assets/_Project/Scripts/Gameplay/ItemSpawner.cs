using System.Collections.Generic;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	public class ItemSpawner : MonoBehaviour
	{
		[FormerlySerializedAs("itemPrefab")]
		[SerializeField] private ItemView m_ItemPrefab;

		private readonly Dictionary<double, ItemView> m_Views = new();
		private IGameState m_State;
		private IBoardCoords m_Board;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			m_Board = BoardCoordsLocator.Current;
			if (m_State == null || m_Board == null)
			{
				Debug.LogError("[ItemSpawner] Locator not ready");
				return;
			}
			m_State.OnItemsChanged += SyncItems;
		}

		private void OnDestroy()
		{
			if (m_State != null) m_State.OnItemsChanged -= SyncItems;
		}

		private void SyncItems()
		{
			var current = new HashSet<double>();
			foreach (var dto in m_State.Items)
			{
				current.Add(dto.Id);
				if (m_Views.ContainsKey(dto.Id)) continue;
				if (m_ItemPrefab == null)
				{
					Debug.LogError("[ItemSpawner] itemPrefab not assigned");
					return;
				}
				var view = Instantiate(m_ItemPrefab, transform);
				view.name = $"Item_{dto.Type}_{dto.Id:F0}";
				view.Bind(dto, m_Board);
				m_Views[dto.Id] = view;
			}

			var stale = new List<double>();
			foreach (var id in m_Views.Keys)
				if (!current.Contains(id)) stale.Add(id);
			foreach (var id in stale)
			{
				Destroy(m_Views[id].gameObject);
				m_Views.Remove(id);
			}
		}
	}
}
