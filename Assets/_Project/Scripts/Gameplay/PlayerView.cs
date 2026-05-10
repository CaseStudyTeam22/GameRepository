using DG.Tweening;
using GamblingAction.Core.Dto;
using GamblingAction.Core.Skills;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	public class PlayerView : MonoBehaviour
	{
		[FormerlySerializedAs("sprite")]
		[SerializeField] private SpriteRenderer m_Sprite;
		[FormerlySerializedAs("baseRenderer")]
		[SerializeField] private Renderer m_BaseRenderer;
		[FormerlySerializedAs("billboardTarget")]
		[SerializeField] private Transform m_BillboardTarget;
		[FormerlySerializedAs("hud")]
		[SerializeField] private PlayerHudView m_Hud;
		[SerializeField, Tooltip("プレイヤー sprite に対する視覚効果（シェイク等）の管理コンポーネント。未設定なら演出は再生されない")]
		private PlayerFxController m_Fx;
		[FormerlySerializedAs("skillSet")]
		[SerializeField, Tooltip("このキャラクターが使うスキル設定（ビジュアルルール含む）。null = SkillPreviewView の fallbackSkillSet を使用")]
		private SkillDefinition m_SkillSet;

		public SkillDefinition SkillSet => m_SkillSet;

		[Header("Movement")]
		[FormerlySerializedAs("moveDuration")]
		[SerializeField] private float m_MoveDuration = 0.22f;
		[FormerlySerializedAs("moveEase")]
		[SerializeField] private Ease m_MoveEase = Ease.OutQuad;

		[Header("Falling (gravity)")]
		[FormerlySerializedAs("gravity")]
		[SerializeField] private float m_Gravity = 18f;
		[FormerlySerializedAs("fallStopY")]
		[SerializeField] private float m_FallStopY = -20f;
		[FormerlySerializedAs("fallKickoffDuration")]
		[SerializeField] private float m_FallKickoffDuration = 0.18f;

		private IGameState m_State;
		private BoardView m_Board;
		private string m_PlayerId;
		private Camera m_Cam;
		private Material m_BaseMaterial;
		private float m_BaseY;
		private Tween m_MoveTween;

		private bool m_IsFalling;
		private float m_FallVelocity;
		private bool m_KickoffDone;

		private bool m_PrevFalling;
		private int m_LastX = int.MinValue;
		private int m_LastY = int.MinValue;

		public string PlayerId => m_PlayerId;

		public void Bind(string playerId, IGameState state, BoardView board)
		{
			m_PlayerId = playerId;
			m_State = state;
			m_Board = board;
			m_Cam = Camera.main;

			if (m_BaseRenderer != null && m_BaseRenderer.sharedMaterial != null)
				m_BaseMaterial = m_BaseRenderer.material;

			m_BaseY = transform.position.y;

			if (m_State.Players.TryGetValue(m_PlayerId, out var dto))
			{
				ApplyColor(dto);
				if (m_Hud != null) m_Hud.Apply(dto);
				SnapTo(dto);
			}

			m_State.OnPlayersChanged += HandlePlayersChanged;
			m_State.OnPhaseChanged   += HandlePhaseChanged;
			m_State.OnGameEvents     += HandleGameEvents;
		}

		private void OnDestroy()
		{
			m_MoveTween?.Kill();
			if (m_State != null)
			{
				m_State.OnPlayersChanged -= HandlePlayersChanged;
				m_State.OnPhaseChanged   -= HandlePhaseChanged;
				m_State.OnGameEvents     -= HandleGameEvents;
			}
		}

		private void HandlePlayersChanged()
		{
			if (!m_State.Players.TryGetValue(m_PlayerId, out var dto)) return;
			ApplyColor(dto);
			ApplyMovement(dto);
			if (m_Hud != null) m_Hud.Apply(dto);
		}

		private void HandlePhaseChanged(EGamePhase phase)
		{
			if (phase == EGamePhase.Exchange)
			{
				if (m_State.Players.TryGetValue(m_PlayerId, out var dto))
					SnapTo(dto);
			}
		}

		private void ApplyMovement(PlayerDto dto)
		{
			if (dto.Falling && !m_PrevFalling)
			{
				StartFalling(dto);
			}
			else if (!dto.Falling && m_PrevFalling)
			{
				m_IsFalling = false;
				m_FallVelocity = 0f;
				m_KickoffDone = false;
			}
			else if (!m_IsFalling && (dto.X != m_LastX || dto.Y != m_LastY))
			{
				var target = m_Board.GridToWorld(dto.X, dto.Y);
				target.y = m_BaseY;
				m_MoveTween?.Kill();
				m_MoveTween = transform.DOMove(target, m_MoveDuration).SetEase(m_MoveEase);
			}

			m_PrevFalling = dto.Falling;
			m_LastX = dto.X;
			m_LastY = dto.Y;
		}

		private void StartFalling(PlayerDto dto)
		{
			m_MoveTween?.Kill();
			m_IsFalling = true;
			m_FallVelocity = 0f;
			m_KickoffDone = false;

			var kickoffTarget = m_Board.GridToWorld(dto.X, dto.Y);
			kickoffTarget.y = m_BaseY;
			m_MoveTween = transform
				.DOMove(kickoffTarget, m_FallKickoffDuration)
				.SetEase(Ease.OutQuad)
				.OnComplete(() => m_KickoffDone = true);
		}

		private void Update()
		{
			if (m_IsFalling && m_KickoffDone && transform.position.y > m_FallStopY)
			{
				m_FallVelocity += m_Gravity * Time.deltaTime;
				var p = transform.position;
				p.y -= m_FallVelocity * Time.deltaTime;
				transform.position = p;
			}
		}

		private void LateUpdate()
		{
			if (m_BillboardTarget == null) return;
			if (m_Cam == null) m_Cam = Camera.main;
			if (m_Cam == null) return;
			m_BillboardTarget.rotation = m_Cam.transform.rotation;
		}

		private void SnapTo(PlayerDto dto)
		{
			m_MoveTween?.Kill();
			m_IsFalling = false;
			m_FallVelocity = 0f;
			m_KickoffDone = false;
			m_PrevFalling = false;

			var pos = m_Board.GridToWorld(dto.X, dto.Y);
			pos.y = m_BaseY;
			transform.position = pos;
			m_LastX = dto.X;
			m_LastY = dto.Y;
		}

		private void ApplyColor(PlayerDto dto)
		{
			var color = ParseColor(dto.Color);
			if (m_Sprite != null) m_Sprite.color = color;
			if (m_BaseMaterial != null) m_BaseMaterial.color = color;
		}

		private static Color ParseColor(string hex)
		{
			return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
		}

		private void HandleGameEvents(EventDto[] events)
		{
			if (events == null || m_Fx == null) return;
			foreach (var ev in events)
			{
				if (ev == null) continue;
				if (ev.TargetId != m_PlayerId) continue;

				if (ev.Type == EventTypes.Hit)
					m_Fx.PlayHitShake();
				else if (ev.Type == EventTypes.Pushed)
					m_Fx.PlayPushedPunch(ev.Dir);
				else if (ev.Type == EventTypes.Vfx && ev.VfxType == VfxTypes.Bump)
					m_Fx.PlayBumpPunch(ev.Dir);
			}
		}
	}
}
