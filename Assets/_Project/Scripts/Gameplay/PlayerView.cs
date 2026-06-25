using DG.Tweening;
using GamblingAction.Core.Dto;
using GamblingAction.Core.Skills;
using GamblingAction.Domain;
using GamblingAction.Gameplay.PopupFx;
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

		[Header("Dust (movement effect)")]
		[SerializeField, Tooltip("移動時に出す土煙の ParticleSystem prefab。null なら土煙は出ない")]
		private ParticleSystem m_DustPrefab;

		[SerializeField, Tooltip("一度に放出する土煙パーティクルの数")]
		private int m_DustEmitCount = 12;
		[SerializeField, Tooltip("移動中、1フレームあたりに放出する土煙の数（軌跡の濃さ）")]
		private int m_DustTrailPerFrame = 2;
		[SerializeField, Range(0f, 1f), Tooltip("移動のこの進行度までで土煙を止める。0.6 なら移動の60%地点までしか出さない（到着マス被り防止）")]
		private float m_DustEmitUntil = 0.6f;

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
		// 土煙はマス移動のたびに生成せず、1個を使い回す（メモリ効率のため）
		private ParticleSystem m_DustInstance;

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

			// PopupDirector に自分を登録（popup の発生位置アンカーとして使われる）
			if (PopupDirector.Instance != null)
				PopupDirector.Instance.RegisterPlayer(m_PlayerId, transform);

			// 登場ディゾルブを再生（m_Fx 未設定なら何もしない）
			m_Fx?.PlayAppear();
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
			// 使い回していた土煙インスタンスを破棄する
			if (m_DustInstance != null) Destroy(m_DustInstance.gameObject);

			if (PopupDirector.Instance != null && !string.IsNullOrEmpty(m_PlayerId))
				PopupDirector.Instance.UnregisterPlayer(m_PlayerId);
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
			/*else if (!m_IsFalling && (dto.X != m_LastX || dto.Y != m_LastY))
			{
				var target = m_Board.GridToWorld(dto.X, dto.Y);
				target.y = m_BaseY;
				m_MoveTween?.Kill();
				m_MoveTween = transform.DOMove(target, m_MoveDuration).SetEase(m_MoveEase);
			}*/
			else if (!m_IsFalling && (dto.X != m_LastX || dto.Y != m_LastY))
			{
				var target = m_Board.GridToWorld(dto.X, dto.Y);
				target.y = m_BaseY;
				m_MoveTween?.Kill();
				m_MoveTween = transform.DOMove(target, m_MoveDuration).SetEase(m_MoveEase)
					.OnUpdate(EmitDustTrail);   // 移動中ずっと足元で土煙を出す
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

	
		/// 足元に土煙を1回放出する。
		/// インスタンスは初回のみ生成し、以降は使い回す。
		private void EmitDust()
		{
			if (m_DustPrefab == null) return;

			// 初回だけ生成する（毎回 Instantiate しないことでメモリ効率を確保）
			if (m_DustInstance == null)
			{
				m_DustInstance = Instantiate(m_DustPrefab, null);
			}

			// 足元の位置に移動させてから放出する
			var footPos = transform.position;
			footPos.y = m_BaseY;
			m_DustInstance.transform.position = footPos;
			m_DustInstance.Emit(m_DustEmitCount);
		}

	
		/// 移動中、現在の足元位置で土煙を少量ずつ放出する（軌跡用）。
		private void EmitDustTrail()
		{
			if (m_DustPrefab == null) return;

			// 移動の終盤（しきい値以降）は放出を止める。到着マスで被らせないため
			if (m_MoveTween != null && m_MoveTween.ElapsedPercentage() > m_DustEmitUntil) return;

			if (m_DustInstance == null)
			{
				m_DustInstance = Instantiate(m_DustPrefab, null);
			}

			// 現在の足元位置に追従させて少量放出する
			var footPos = transform.position;
			footPos.y = m_BaseY;
			m_DustInstance.transform.position = footPos;
			m_DustInstance.Emit(m_DustTrailPerFrame);
		}

		private void ApplyColor(PlayerDto dto)
		{
			var color = ParseColor(dto.Color);
			if (m_Sprite != null) m_Sprite.color = color;
			if (m_BaseMaterial != null) m_BaseMaterial.color = color;
			// 登場ディゾルブの境界発光色もチーム色に合わせる
			m_Fx?.SetEdgeColor(color);
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
