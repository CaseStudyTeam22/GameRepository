using DG.Tweening;
using GamblingAction.Core.Dto;
using GamblingAction.Core.Skills;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	public class PlayerView : MonoBehaviour
	{
		[SerializeField] SpriteRenderer sprite;
		[SerializeField] Renderer baseRenderer;
		[SerializeField] Transform billboardTarget;
		[SerializeField] PlayerHudView hud;
		[SerializeField, Tooltip("このキャラクターが使うスキル設定（ビジュアルルール含む）。null = SkillPreviewView の fallbackSkillSet を使用")]
		SkillDefinition skillSet;

		public SkillDefinition SkillSet => skillSet;

		[Header("Movement")]
		[SerializeField] float moveDuration = 0.22f;
		[SerializeField] Ease moveEase = Ease.OutQuad;

		[Header("Falling (gravity)")]
		[SerializeField] float gravity = 18f;
		[SerializeField] float fallStopY = -20f;
		[SerializeField] float fallKickoffDuration = 0.18f;

		IGameState _state;
		BoardView _board;
		string _playerId;
		Camera _cam;
		Material _baseMaterial;
		float _baseY;
		Tween _moveTween;

		bool _isFalling;
		float _fallVelocity;
		bool _kickoffDone;

		bool _prevFalling;
		int _lastX = int.MinValue;
		int _lastY = int.MinValue;

		public string PlayerId => _playerId;

		public void Bind(string playerId, IGameState state, BoardView board)
		{
			_playerId = playerId;
			_state = state;
			_board = board;
			_cam = Camera.main;

			if (baseRenderer != null && baseRenderer.sharedMaterial != null)
				_baseMaterial = baseRenderer.material;

			_baseY = transform.position.y;

			if (_state.Players.TryGetValue(_playerId, out var dto))
			{
				ApplyColor(dto);
				if (hud != null) hud.Apply(dto);
				SnapTo(dto);
			}

			_state.OnPlayersChanged += HandlePlayersChanged;
			_state.OnPhaseChanged   += HandlePhaseChanged;
		}

		void OnDestroy()
		{
			_moveTween?.Kill();
			if (_state != null)
			{
				_state.OnPlayersChanged -= HandlePlayersChanged;
				_state.OnPhaseChanged   -= HandlePhaseChanged;
			}
		}

		void HandlePlayersChanged()
		{
			if (!_state.Players.TryGetValue(_playerId, out var dto)) return;
			ApplyColor(dto);
			ApplyMovement(dto);
			if (hud != null) hud.Apply(dto);
		}

		void HandlePhaseChanged(GamePhase phase)
		{
			if (phase == GamePhase.Exchange)
			{
				if (_state.Players.TryGetValue(_playerId, out var dto))
					SnapTo(dto);
			}
		}

		void ApplyMovement(PlayerDto dto)
		{
			if (dto.Falling && !_prevFalling)
			{
				StartFalling(dto);
			}
			else if (!dto.Falling && _prevFalling)
			{
				_isFalling = false;
				_fallVelocity = 0f;
				_kickoffDone = false;
			}
			else if (!_isFalling && (dto.X != _lastX || dto.Y != _lastY))
			{
				var target = _board.GridToWorld(dto.X, dto.Y);
				target.y = _baseY;
				_moveTween?.Kill();
				_moveTween = transform.DOMove(target, moveDuration).SetEase(moveEase);
			}

			_prevFalling = dto.Falling;
			_lastX = dto.X;
			_lastY = dto.Y;
		}

		void StartFalling(PlayerDto dto)
		{
			_moveTween?.Kill();
			_isFalling = true;
			_fallVelocity = 0f;
			_kickoffDone = false;

			var kickoffTarget = _board.GridToWorld(dto.X, dto.Y);
			kickoffTarget.y = _baseY;
			_moveTween = transform
				.DOMove(kickoffTarget, fallKickoffDuration)
				.SetEase(Ease.OutQuad)
				.OnComplete(() => _kickoffDone = true);
		}

		void Update()
		{
			if (_isFalling && _kickoffDone && transform.position.y > fallStopY)
			{
				_fallVelocity += gravity * Time.deltaTime;
				var p = transform.position;
				p.y -= _fallVelocity * Time.deltaTime;
				transform.position = p;
			}
		}

		void LateUpdate()
		{
			if (billboardTarget == null) return;
			if (_cam == null) _cam = Camera.main;
			if (_cam == null) return;
			billboardTarget.rotation = _cam.transform.rotation;
		}

		void SnapTo(PlayerDto dto)
		{
			_moveTween?.Kill();
			_isFalling = false;
			_fallVelocity = 0f;
			_kickoffDone = false;
			_prevFalling = false;

			var pos = _board.GridToWorld(dto.X, dto.Y);
			pos.y = _baseY;
			transform.position = pos;
			_lastX = dto.X;
			_lastY = dto.Y;
		}

		void ApplyColor(PlayerDto dto)
		{
			var color = ParseColor(dto.Color);
			if (sprite != null) sprite.color = color;
			if (_baseMaterial != null) _baseMaterial.color = color;
		}

		static Color ParseColor(string hex)
		{
			return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
		}
	}
}
