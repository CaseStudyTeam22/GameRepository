using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	public class ItemView : MonoBehaviour
	{
		[FormerlySerializedAs("sprite")]
		[SerializeField] private SpriteRenderer m_Sprite;
		[FormerlySerializedAs("chipsColor")]
		[SerializeField] private Color m_ChipsColor = new(1f, 0.85f, 0.1f);
		[FormerlySerializedAs("moneyColor")]
		[SerializeField] private Color m_MoneyColor = new(0.2f, 0.9f, 0.4f);
		[FormerlySerializedAs("billboardTarget")]
		[SerializeField] private Transform m_BillboardTarget;
		[FormerlySerializedAs("bobAmplitude")]
		[SerializeField] private float m_BobAmplitude = 0.1f;
		[FormerlySerializedAs("bobSpeed")]
		[SerializeField] private float m_BobSpeed = 2f;

		private IBoardCoords m_Board;
		private Camera m_Cam;
		private ItemDto m_Dto;
		private Vector3 m_BasePos;

		public double ItemId => m_Dto?.Id ?? 0;

		public void Bind(ItemDto dto, IBoardCoords board)
		{
			m_Dto = dto;
			m_Board = board;
			m_Cam = Camera.main;

			ApplyColor();
			m_BasePos = m_Board.GridToWorld(dto.X, dto.Y) + Vector3.up * 0.4f;
			transform.position = m_BasePos;
		}

		private void ApplyColor()
		{
			if (m_Sprite == null) return;
			m_Sprite.color = m_Dto.Type == "chips" ? m_ChipsColor : m_MoneyColor;
		}

		private void Update()
		{
			transform.position = m_BasePos + Vector3.up * Mathf.Sin(Time.time * m_BobSpeed) * m_BobAmplitude;
		}

		private void LateUpdate()
		{
			if (m_BillboardTarget == null) return;
			if (m_Cam == null) m_Cam = Camera.main;
			if (m_Cam == null) return;
			m_BillboardTarget.rotation = m_Cam.transform.rotation;
		}
	}
}
