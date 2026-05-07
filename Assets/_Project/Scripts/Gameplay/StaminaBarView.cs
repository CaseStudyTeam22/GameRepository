using GamblingAction.Core;
using GamblingAction.Core.Dto;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	public class StaminaBarView : MonoBehaviour
	{
		[Header("References (set in prefab)")]
		[FormerlySerializedAs("cellTemplate")]
		[SerializeField] private Image m_CellTemplate;
		[FormerlySerializedAs("cellsRoot")]
		[SerializeField] private Transform m_CellsRoot;

		[Header("Colors")]
		[FormerlySerializedAs("healthyColor")]
		[SerializeField] private Color m_HealthyColor = new(0f, 1f, 0f);
		[FormerlySerializedAs("lowColor")]
		[SerializeField] private Color m_LowColor = new(1f, 0.27f, 0.27f);
		[FormerlySerializedAs("emptyColor")]
		[SerializeField] private Color m_EmptyColor = new(0.13f, 0.13f, 0.13f, 1f);
		[FormerlySerializedAs("lowThreshold")]
		[SerializeField] private int m_LowThreshold = 1;

		private Image[] m_Cells;
		private int m_LastStamina = -1;

		private void Awake()
		{
			BuildCellsFromTemplate();
		}

		private void BuildCellsFromTemplate()
		{
			if (m_CellTemplate == null)
			{
				Debug.LogError("[StaminaBar] cellTemplate not assigned");
				return;
			}
			Transform parent = m_CellsRoot != null ? m_CellsRoot : m_CellTemplate.transform.parent;

			int n = GameConfig.MaxStamina;
			m_Cells = new Image[n];

			for (int i = 0; i < n; i++)
			{
				var clone = Instantiate(m_CellTemplate, parent);
				clone.gameObject.name = $"Cell{i}";
				clone.gameObject.SetActive(true);
				m_Cells[i] = clone;
			}

			m_CellTemplate.gameObject.SetActive(false);
		}

		public void Apply(PlayerDto dto)
		{
			if (dto == null || m_Cells == null) return;
			if (dto.Stamina == m_LastStamina) return;
			m_LastStamina = dto.Stamina;

			var fillColor = dto.Stamina <= m_LowThreshold ? m_LowColor : m_HealthyColor;
			for (int i = 0; i < m_Cells.Length; i++)
			{
				bool filled = i < dto.Stamina;
				m_Cells[i].color = filled ? fillColor : m_EmptyColor;
			}
		}
	}
}
