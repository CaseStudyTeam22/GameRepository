using GamblingAction.Core.Dto;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	public class PlayerHudView : MonoBehaviour
	{
		[Header("Sub-views (set in prefab)")]
		[SerializeField] StaminaBarView staminaBar;
		// [SerializeField] NameLabelView  nameLabel;
		// [SerializeField] AITagView       aiTag;
		// [SerializeField] BuffIconsView   buffIcons;

		[Header("Camera")]
		[SerializeField] bool billboardToCamera = true;

		Camera _cam;

		void Awake()
		{
			_cam = Camera.main;
		}

		public void Apply(PlayerDto dto)
		{
			if (dto == null) return;
			if (staminaBar != null) staminaBar.Apply(dto);
		}

		void LateUpdate()
		{
			if (!billboardToCamera) return;
			if (_cam == null) _cam = Camera.main;
			if (_cam == null) return;
			transform.rotation = _cam.transform.rotation;
		}
	}
}
