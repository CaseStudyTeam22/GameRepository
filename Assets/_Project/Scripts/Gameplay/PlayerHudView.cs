using GamblingAction.Core.Dto;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	public class PlayerHudView : MonoBehaviour
	{
		[Header("Sub-views (set in prefab)")]
		[FormerlySerializedAs("staminaBar")]
		[SerializeField] private StaminaBarView m_StaminaBar;

		[Header("Camera")]
		[FormerlySerializedAs("billboardToCamera")]
		[SerializeField] private bool m_BillboardToCamera = true;

		private Camera m_Cam;

		private void Awake()
		{
			m_Cam = Camera.main;
		}

		public void Apply(PlayerDto dto)
		{
			if (dto == null) return;
			if (m_StaminaBar != null) m_StaminaBar.Apply(dto);
		}

		private void LateUpdate()
		{
			if (!m_BillboardToCamera) return;
			if (m_Cam == null) m_Cam = Camera.main;
			if (m_Cam == null) return;
			transform.rotation = m_Cam.transform.rotation;
		}
	}
}
