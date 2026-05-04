using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	public class ItemView : MonoBehaviour
	{
		[SerializeField] SpriteRenderer sprite;
		[SerializeField] Color chipsColor = new(1f, 0.85f, 0.1f);
		[SerializeField] Color moneyColor = new(0.2f, 0.9f, 0.4f);
		[SerializeField] Transform billboardTarget;
		[SerializeField] float bobAmplitude = 0.1f;
		[SerializeField] float bobSpeed = 2f;

		IBoardCoords _board;
		Camera _cam;
		ItemDto _dto;
		Vector3 _basePos;

		public double ItemId => _dto?.Id ?? 0;

		public void Bind(ItemDto dto, IBoardCoords board)
		{
			_dto = dto;
			_board = board;
			_cam = Camera.main;

			ApplyColor();
			_basePos = _board.GridToWorld(dto.X, dto.Y) + Vector3.up * 0.4f;
			transform.position = _basePos;
		}

		void ApplyColor()
		{
			if (sprite == null) return;
			sprite.color = _dto.Type == "chips" ? chipsColor : moneyColor;
		}

		void Update()
		{
			transform.position = _basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
		}

		void LateUpdate()
		{
			if (billboardTarget == null) return;
			if (_cam == null) _cam = Camera.main;
			if (_cam == null) return;
			billboardTarget.rotation = _cam.transform.rotation;
		}
	}
}
