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
		[SerializeField, Tooltip("相手のインテントを表示する吹き出し。")]
		private OpponentIntentBubbleView m_IntentBubble;

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

		[Header("Narikin skill trail (gold)")]
		[SerializeField, Tooltip("成金スキル時に線を引く Trail Renderer（Player 直下の GoldTrail）。普段は emitting=false")]
		private TrailRenderer m_GoldTrail;

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
		// 土煙はマス移動のたびに生成せず、1個を使い回す
		private ParticleSystem m_DustInstance;

		// 次の移動を金色トレイルにするフラグ（成金スキルVFX受信で立つ）
		private bool m_NarikinTrailPending;

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

			if (m_PlayerId != m_State.MyId)
			{
				m_State.OnOpponentIntentRevealed += HandleOpponentIntentRevealed;
			}

			// PopupDirector に自分を登録（popup の発生位置アンカーとして使われる）
			if (PopupDirector.Instance != null)
				PopupDirector.Instance.RegisterPlayer(m_PlayerId, transform);

			// 登場ディゾルブを再生（m_Fx 未設定なら何もしない）
			m_Fx?.PlayAppear();

			// 金色トレイルは普段オフ（スキル移動時だけオンにする）
			if (m_GoldTrail != null)
			{
				m_GoldTrail.emitting = false;
				m_GoldTrail.Clear();
			}
		}

		private void OnDestroy()
		{
			m_MoveTween?.Kill();
			if (m_State != null)
			{
				m_State.OnPlayersChanged -= HandlePlayersChanged;
				m_State.OnPhaseChanged   -= HandlePhaseChanged;
				m_State.OnGameEvents     -= HandleGameEvents;

				if (m_PlayerId != m_State.MyId)
				{
					m_State.OnOpponentIntentRevealed -= HandleOpponentIntentRevealed;
				}
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

			if (m_PlayerId != m_State.MyId && m_IntentBubble != null)
			{
				// イカサマ状態による意図の可視化は同期処理(dto.Intent == null)で消去しないようにします。
				// 吹き出しの表示・非表示は HandleOpponentIntentRevealed メッセージでのみ制御します。
			}
		}

		private void HandlePhaseChanged(EGamePhase phase)
		{
			if (phase == EGamePhase.Exchange)
			{
				if (m_State.Players.TryGetValue(m_PlayerId, out var dto))
					SnapTo(dto);
			}

			if (m_PlayerId != m_State.MyId && m_IntentBubble != null)
			{
				m_IntentBubble.Hide();
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

				// 成金スキル発動直後の移動なら、金色トレイルの線引きを開始する
				bool goldTrail = m_NarikinTrailPending;
				if (goldTrail && m_GoldTrail != null)
				{
					m_GoldTrail.Clear();          // 前回の線が残っていたら消してから
					m_GoldTrail.emitting = true;  // 線を引き始める
				}

				m_MoveTween = transform.DOMove(target, m_MoveDuration).SetEase(m_MoveEase)
					.OnUpdate(EmitDustTrail)      // 通常の土煙は従来どおり
					.OnComplete(() =>
					{
						if (goldTrail && m_GoldTrail != null)
						{
							m_GoldTrail.emitting = false;  // 線引き終了（既存の線は time 秒かけて消える）
						}
						m_NarikinTrailPending = false;
					});
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

			// 位置を瞬間移動させるときは、線が飛んで繋がるのを防ぐためクリアする
			if (m_GoldTrail != null)
			{
				m_GoldTrail.emitting = false;
				m_GoldTrail.Clear();
			}
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
				else if (ev.Type == EventTypes.Vfx &&
					(ev.VfxType == VfxTypes.RestVfx ||
					 ev.VfxType == VfxTypes.AttackVfx ||
					 ev.VfxType == VfxTypes.DefenseVfx))
				{
					// スキル起因の VFX を検知したら、スキルに応じたエフェクトを出す。
					if (m_State.Players.TryGetValue(m_PlayerId, out var dto))
					{
						int idx = SkillIdToIndex(dto.SkillData != null ? dto.SkillData.Id : null);
						if (idx == 2)
						{
							// 成金スキル：一発エフェクトは出さず、次の移動で金色トレイルを引く
							m_NarikinTrailPending = true;
						}
						else
						{
							// 格闘家含め、スキルは向きを持たない（3x3全方位攻撃）ため dir は渡さない
							m_Fx.PlaySkill(idx);
						}
					}
				}
			}
		}

		// サーバーの skillData.id を、PlayerFxController の配列添字に変換する。
		// 添字はキャラ番号（1=医師〜6=債務者）に合わせてある。0 = 不明でエフェクト無し。
		private static int SkillIdToIndex(string skillId)
		{
			switch (skillId)
			{
				case "heal_instant":      return 1; // 医師
				case "nouveau_skill":     return 2; // 成金（サーバー実装によってはこちら）
				case "double_cost_power": return 2; // 成金（クライアント設定はこちら）
				case "fighter_skill":     return 3; // 格闘家
				case "guardian_skill":    return 4; // ガーディアン
				case "scammer_skill":     return 5; // イカサマ
				case "debtor_skill":      return 6; // 債務者
				default:                  return 0; // 不明 = エフェクト無し
			}
		}

		private void HandleOpponentIntentRevealed(OpponentIntentRevealedMessage msg)
		{
			if (m_PlayerId == m_State.MyId) return; // 自分自身のインテントは表示しない（相手のものだけ表示する）

			if (msg != null && msg.Intent != null)
			{
				if (m_IntentBubble != null)
				{
					if (msg.Intent.Type == "none")
					{
						m_IntentBubble.Hide();
					}
					else
					{
						string resolvedSkillId = msg.Intent.Type;
						if (msg.Intent.Type == "skill")
						{
							int opponentCharaIndex = -1;
							if (m_State.Players.TryGetValue(m_PlayerId, out var oppDto))
							{
								opponentCharaIndex = oppDto.CharaIndex;
							}
							resolvedSkillId = GetSkillIdForChara(opponentCharaIndex);
						}
						m_IntentBubble.ShowIntent(resolvedSkillId);
					}
				}
			}
		}

		private string GetSkillIdForChara(int charaIndex)
		{
			switch (charaIndex)
			{
				case 0: return "guardian_skill";
				case 1: return "doctor_skill";
				case 2: return "nouveau_riche_skill";
				case 3: return "fighter_skill";
				case 4: return "guardian_skill";
				case 5: return "scammer_skill";
				case 6: return "debtor_skill";
				default: return "skill";
			}
		}
	}
}