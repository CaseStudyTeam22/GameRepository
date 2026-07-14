using GamblingAction.Core;
using GamblingAction.Core.Dto;
using System.Collections.Generic;
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

		private List<Image> m_Cells = new();
		private List<GameObject> m_BgCells = new();
		private int m_LastStamina = -1;
		private int m_LastMaxStamina = -1;

		private void Awake()
		{
			if (m_CellTemplate != null)
			{
				m_CellTemplate.gameObject.SetActive(false);
			}
		}

		public void Apply(PlayerDto dto)
		{
			if (dto == null) return;

			// 現在の最大スタミナ（サーバー側で計算されたバフ・補正適用後の値）
			int currentMax = dto.CurrentMaxStamina;

			// 最大スタミナの変動を確認
			if (currentMax != m_LastMaxStamina)
			{
				UpdateMaxStamina(currentMax);
				m_LastMaxStamina = currentMax;
			}

			// スタミナの更新、あるいは最大スタミナ更新時に再描画
			UpdateStaminaVisuals(dto.Stamina, currentMax);

			m_LastStamina = dto.Stamina;
		}

		// 最大スタミナに応じてセルの数を増減
		private void UpdateMaxStamina(int newMax)
		{
			if (m_CellTemplate == null) return;

			Transform parent = m_CellsRoot != null ? m_CellsRoot : m_CellTemplate.transform.parent;

			// スタミナ2 = 1メモリなので、セル数は切り上げ
			int targetCellCount = Mathf.CeilToInt(newMax / 2f);

			// 不足しているセル（背景＋前面）を生成
			while (m_Cells.Count < targetCellCount)
			{
				int index = m_Cells.Count;

				// 背景セルをテンプレートから生成
				var bgClone = Instantiate(m_CellTemplate, parent);
				bgClone.gameObject.name = $"CellBg{index}";
				bgClone.gameObject.SetActive(true);
				bgClone.color = m_EmptyColor;

				// 前面セルを背景セルの子として生成
				var fgClone = Instantiate(m_CellTemplate, bgClone.transform);
				fgClone.gameObject.name = $"CellFg{index}";
				fgClone.gameObject.SetActive(true);

				// 前面セルの RectTransform を Stretch Stretch に設定
				var rectTrans = fgClone.GetComponent<RectTransform>();
				if (rectTrans != null)
				{
					rectTrans.anchorMin = Vector2.zero;
					rectTrans.anchorMax = Vector2.one;
					rectTrans.offsetMin = Vector2.zero;
					rectTrans.offsetMax = Vector2.zero;
				}

				// 前面セルの Image.Type を Filled に、FillMethod を Horizontal に設定
				fgClone.type = Image.Type.Filled;
				fgClone.fillMethod = Image.FillMethod.Horizontal;
				fgClone.fillAmount = 0f;

				m_BgCells.Add(bgClone.gameObject);
				m_Cells.Add(fgClone);
			}

			// 最大値に合わせて表示/非表示を切り替え
			for (int i = 0; i < m_BgCells.Count; i++)
			{
				bool isActive = i < targetCellCount;
				m_BgCells[i].SetActive(isActive);
			}
		}

		// 見た目の変更
		private void UpdateStaminaVisuals(int stamina, int maxStamina)
		{
			var fillColor = stamina <= m_LowThreshold ? m_LowColor : m_HealthyColor;
			int targetCellCount = Mathf.CeilToInt(maxStamina / 2f);

			for (int i = 0; i < m_Cells.Count; i++)
			{
				if (i >= targetCellCount)
				{
					m_Cells[i].fillAmount = 0f;
					continue;
				}

				// 各メモリはスタミナ 2 個分を表す
				int requiredForHalf = i * 2 + 1;
				int requiredForFull = i * 2 + 2;

				if (stamina >= requiredForFull)
				{
					m_Cells[i].fillAmount = 1.0f;
					m_Cells[i].color = fillColor;
				}
				else if (stamina == requiredForHalf)
				{
					m_Cells[i].fillAmount = 0.5f;
					m_Cells[i].color = fillColor;
				}
				else
				{
					m_Cells[i].fillAmount = 0f;
				}
			}
		}
	}
}
