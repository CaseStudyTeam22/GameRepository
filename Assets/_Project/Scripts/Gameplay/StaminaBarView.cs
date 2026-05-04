using GamblingAction.Core;
using GamblingAction.Core.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	public class StaminaBarView : MonoBehaviour
	{
		[Header("References (set in prefab)")]
		[SerializeField] Image cellTemplate;
		[SerializeField] Transform cellsRoot;

		[Header("Colors")]
		[SerializeField] Color healthyColor = new(0f, 1f, 0f);
		[SerializeField] Color lowColor = new(1f, 0.27f, 0.27f);
		[SerializeField] Color emptyColor = new(0.13f, 0.13f, 0.13f, 1f);
		[SerializeField] int lowThreshold = 1;

		Image[] _cells;
		int _lastStamina = -1;

		void Awake()
		{
			BuildCellsFromTemplate();
		}

		void BuildCellsFromTemplate()
		{
			if (cellTemplate == null)
			{
				Debug.LogError("[StaminaBar] cellTemplate not assigned");
				return;
			}
			Transform parent = cellsRoot != null ? cellsRoot : cellTemplate.transform.parent;

			int n = GameConfig.MaxStamina;
			_cells = new Image[n];

			for (int i = 0; i < n; i++)
			{
				var clone = Instantiate(cellTemplate, parent);
				clone.gameObject.name = $"Cell{i}";
				clone.gameObject.SetActive(true);
				_cells[i] = clone;
			}

			cellTemplate.gameObject.SetActive(false);
		}

		public void Apply(PlayerDto dto)
		{
			if (dto == null || _cells == null) return;
			if (dto.Stamina == _lastStamina) return;
			_lastStamina = dto.Stamina;

			var fillColor = dto.Stamina <= lowThreshold ? lowColor : healthyColor;
			for (int i = 0; i < _cells.Length; i++)
			{
				bool filled = i < dto.Stamina;
				_cells[i].color = filled ? fillColor : emptyColor;
			}
		}
	}
}
