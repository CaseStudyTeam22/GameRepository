using System.Collections;
using DG.Tweening;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	// Lobby シーンの準備画面。左=自分・右=相手で固定表示する。
	// 各 UI 要素はブロック単位で割り当て、内部は名前で取得する。
	// 準備ボタンで SubmitReady / SubmitUnready を送り、双方準備完了で
	// カウントダウンを出し、サーバが Lobby を抜けたら Boot へ遷移する。
	public class LobbyController : MonoBehaviour
	{
		[Header("Blocks")]
		[SerializeField, Tooltip("Boot へ遷移する SceneLoader")]
		private SceneLoader m_SceneLoader;
		[SerializeField, Tooltip("自分側パネル（左）。内部に NamePanel/NameText と StatePanel/ReadyStateText を持つ")]
		private GameObject m_SelfPanel;
		[SerializeField, Tooltip("相手側パネル（右）。構造は自分側と同じ")]
		private GameObject m_OpponentPanel;
		[SerializeField, Tooltip("上部の P1/P2 ラベルを持つパネル")]
		private GameObject m_PlayerSidePanel;
		[SerializeField, Tooltip("全体状況テキスト（Stand By / カウントダウン）")]
		private TMP_Text m_GlobalStateText;
		[SerializeField, Tooltip("準備パネル。ReadyButton / ReadyAsAIToggle / CharaOption を持つ")]
		private GameObject m_PreparingPanel;

		[Header("Colors")]
		[SerializeField] private Color m_P1Color = new Color(0f, 242f / 255f, 254f / 255f);
		[SerializeField] private Color m_P2Color = new Color(1f, 126f / 255f, 0f);
		[SerializeField] private Color m_ReadyBgColor = new Color(0.9f, 0.18f, 0.18f);
		[SerializeField] private Color m_WaitingBgColor = Color.black;

		[Header("Tuning")]
		[SerializeField, Tooltip("自分側 P ラベルのフォントサイズ")]
		private float m_SelfLabelSize = 48f;
		[SerializeField, Tooltip("相手側 P ラベルのフォントサイズ")]
		private float m_OpponentLabelSize = 32f;
		[SerializeField, Tooltip("選択中キャラボタンの拡大率")]
		private float m_CharaSelectedScale = 1.12f;
		[SerializeField, Tooltip("カウントダウン 1 ステップの秒数")]
		private float m_CountdownStep = 0.8f;

		[Header("入場")]
		[SerializeField, Tooltip("各要素が画面外から滑り込む距離（px）")]
		private float m_SlideDistance = 800f;
		[SerializeField, Tooltip("1 要素の滑り込みの長さ（秒）")]
		private float m_SlideDuration = 0.5f;
		[SerializeField, Tooltip("名札→BG→準備状態の開始間隔（秒）")]
		private float m_SlideStagger = 0.12f;
		[SerializeField, Tooltip("滑り込みのイージング")]
		private Ease m_SlideEase = Ease.OutQuad;

		[Header("キャラ切替（自分側 Portrait）")]
		[SerializeField, Tooltip("キャラ立ち絵。index 0=ランダム（初期表示）, 1=A, 2=B, 3=C")]
		private Sprite[] m_CharaSprites;
		[SerializeField, Tooltip("退場時に旧絵が外側（左）へ退く距離（px）")]
		private float m_CharaExitOffset = 60f;
		[SerializeField, Tooltip("入場時に新絵が内側（右）から滑り込む距離（px）")]
		private float m_CharaEnterOffset = 120f;
		[SerializeField, Tooltip("切替アニメの長さ（秒）")]
		private float m_CharaSwapDuration = 0.3f;
		[SerializeField, Tooltip("切替のイージング")]
		private Ease m_CharaSwapEase = Ease.OutQuad;

		private IGameState m_State;

		// ブロック内から名前で取得した要素。
		private TMP_Text m_SelfNameText, m_SelfStateText;
		private Image m_SelfStateBg, m_SelfNameBg;
		private TMP_Text m_OpponentNameText, m_OpponentStateText;
		private Image m_OpponentStateBg, m_OpponentNameBg;
		// 左ラベル=自分側、右ラベル=相手側。中身（P1/P2）と色は動的に設定する。
		private TMP_Text m_LeftLabel, m_RightLabel;
		private Button m_ReadyButton;
		private TMP_Text m_ReadyButtonText;
		private Toggle m_ReadyAsAIToggle;
		private Button[] m_CharaButtons;
		// ランダム選択ボタン。立ち絵 index 0 に対応する。
		private Button m_RandomButton;

		// 自分側 Portrait のキャラ切替用の 2 枚スロット。旧絵を退場させつつ新絵を入場させる。
		private Image m_SelfPortraitSlotA, m_SelfPortraitSlotB;
		// 現在表示中のスロット。切替のたびにもう一方と入れ替える。
		private Image m_SelfActiveSlot, m_SelfIdleSlot;
		// スロットの定位置（中心）。退場・入場はここを基準に左右へずらす。
		private float m_SelfSlotHomeX;
		// 現在選択中のキャラ index。0=ランダム。同じボタンの連打を無視するのにも使う。
		private int m_SelfCharaIndex;

		// 各サイドの滑り込み対象。順序 = 名札 → BG(Portrait) → 準備状態。
		private RectTransform[] m_SelfSlideItems, m_OpponentSlideItems;
		private Vector3[] m_SelfSlideHomes, m_OpponentSlideHomes;
		private bool m_SelfJoined, m_OpponentJoined;

		private bool m_IsReady;
		private Coroutine m_CountdownCo;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[Lobby] GameStateLocator.Current is null");
				return;
			}

			FindElements();

			if (m_ReadyButton != null)
				m_ReadyButton.onClick.AddListener(OnReadyClicked);
			WireCharaButtons();

			m_State.OnStateInitialized += Refresh;
			m_State.OnPlayersChanged   += Refresh;
			m_State.OnCountdownStart   += HandleCountdownStart;
			m_State.OnCountdownCancel  += HandleCountdownCancel;

			// Lobby に入ったことをサーバへ通知する（相手側の滑り込み起点）。
			m_State.SubmitEnterLobby();

			Refresh();
		}

		private void OnDestroy()
		{
			// 切替アニメが残っていると破棄済み Image を触って例外になるため止める。
			if (m_SelfPortraitSlotA != null) m_SelfPortraitSlotA.DOKill();
			if (m_SelfPortraitSlotB != null) m_SelfPortraitSlotB.DOKill();

			if (m_State == null) return;
			m_State.OnStateInitialized -= Refresh;
			m_State.OnPlayersChanged   -= Refresh;
			m_State.OnCountdownStart   -= HandleCountdownStart;
			m_State.OnCountdownCancel  -= HandleCountdownCancel;
		}

		// 各ブロック内の要素を名前で取得する。
		private void FindElements()
		{
			m_SelfNameText  = Find<TMP_Text>(m_SelfPanel, "NamePanel/NameText");
			m_SelfNameBg    = Find<Image>(m_SelfPanel,    "NamePanel");
			m_SelfStateText = Find<TMP_Text>(m_SelfPanel, "StatePanel/ReadyStateText");
			m_SelfStateBg   = Find<Image>(m_SelfPanel,    "StatePanel");

			m_OpponentNameText  = Find<TMP_Text>(m_OpponentPanel, "NamePanel/NameText");
			m_OpponentNameBg    = Find<Image>(m_OpponentPanel,    "NamePanel");
			m_OpponentStateText = Find<TMP_Text>(m_OpponentPanel, "StatePanel/ReadyStateText");
			m_OpponentStateBg   = Find<Image>(m_OpponentPanel,    "StatePanel");

			// P1 物体=左ラベル、P2 物体=右ラベルとして扱う。中身は動的。
			m_LeftLabel  = Find<TMP_Text>(m_PlayerSidePanel, "P1");
			m_RightLabel = Find<TMP_Text>(m_PlayerSidePanel, "P2");

			m_ReadyButton     = Find<Button>(m_PreparingPanel,  "ReadyButton");
			m_ReadyButtonText = Find<TMP_Text>(m_PreparingPanel, "ReadyButton/Text (TMP)");
			m_ReadyAsAIToggle = Find<Toggle>(m_PreparingPanel,  "ReadyAsAIToggle");

			m_CharaButtons = new[]
			{
				Find<Button>(m_PreparingPanel, "CharaOption/Chara_A"),
				Find<Button>(m_PreparingPanel, "CharaOption/Chara_B"),
				Find<Button>(m_PreparingPanel, "CharaOption/Chara_C"),
			};
			m_RandomButton = Find<Button>(m_PreparingPanel, "CharaOption/Chara_Random");

			// 自分側 Portrait の 2 枚スロットを取得し、初期状態（ランダム絵）を作る。
			m_SelfPortraitSlotA = Find<Image>(m_SelfPanel, "Portrait/SlotA");
			m_SelfPortraitSlotB = Find<Image>(m_SelfPanel, "Portrait/SlotB");
			InitCharaSlots();

			// 滑り込み対象を順序どおり集める：名札 → BG(Portrait) → 準備状態。
			m_SelfSlideItems = CollectSlideItems(m_SelfPanel);
			m_OpponentSlideItems = CollectSlideItems(m_OpponentPanel);
			m_SelfSlideHomes = RememberHomes(m_SelfSlideItems);
			m_OpponentSlideHomes = RememberHomes(m_OpponentSlideItems);

			// 自分は必ず滑り込ませるので画面外（左）へ退避する。
			OffscreenItems(m_SelfSlideItems, m_SelfSlideHomes, -m_SlideDistance);

			// 自分が入室した時点で相手が既に在室なら滑らせず終点に置く（処理済み扱い）。
			// それ以外は画面外（右）へ退避し、後で滑り込ませる。
			var opponent = m_State.Opponent;
			if (opponent != null && opponent.InLobby)
				m_OpponentJoined = true;
			else
				OffscreenItems(m_OpponentSlideItems, m_OpponentSlideHomes, m_SlideDistance);
		}

		// 名札 → BG → 準備状態 の順で RectTransform を集める。
		private RectTransform[] CollectSlideItems(GameObject sidePanel)
		{
			return new[]
			{
				Find<RectTransform>(sidePanel, "NamePanel"),
				Find<RectTransform>(sidePanel, "Portrait"),
				Find<RectTransform>(sidePanel, "StatePanel"),
			};
		}

		private static Vector3[] RememberHomes(RectTransform[] items)
		{
			var homes = new Vector3[items.Length];
			for (int i = 0; i < items.Length; i++)
				if (items[i] != null) homes[i] = items[i].localPosition;
			return homes;
		}

		private static void OffscreenItems(RectTransform[] items, Vector3[] homes, float offsetX)
		{
			for (int i = 0; i < items.Length; i++)
				if (items[i] != null)
					items[i].localPosition = homes[i] + new Vector3(offsetX, 0f, 0f);
		}

		private static T Find<T>(GameObject root, string path) where T : Component
		{
			if (root == null) return null;
			var t = root.transform.Find(path);
			if (t == null)
			{
				Debug.LogWarning($"[Lobby] '{path}' not found under {root.name}");
				return null;
			}
			var c = t.GetComponent<T>();
			if (c == null) Debug.LogWarning($"[Lobby] '{path}' has no {typeof(T).Name}");
			return c;
		}

		private void OnReadyClicked()
		{
			if (!m_IsReady)
			{
				bool isAI = m_ReadyAsAIToggle != null && m_ReadyAsAIToggle.isOn;
				m_State.SubmitReady(isAI);
				m_IsReady = true;
			}
			else
			{
				m_State.SubmitUnready();
				m_IsReady = false;
			}
			UpdateReadyButtonText();
		}

		private void UpdateReadyButtonText()
		{
			if (m_ReadyButtonText != null)
				m_ReadyButtonText.text = m_IsReady ? "Cancel" : "I'm Ready";
		}

		// 名前色・準備状態・上部ラベル・全体状況をまとめて反映する。
		private void Refresh()
		{
			var me = m_State.Me;
			var opponent = m_State.Opponent;

			// 自分は Lobby に入った時点で滑り込ませる。
			if (me != null && !m_SelfJoined)
			{
				m_SelfJoined = true;
				SlideInSide(m_SelfSlideItems, m_SelfSlideHomes);
			}
			// 相手は Lobby に入った（inLobby）ときだけ滑り込ませる。接続だけでは出さない。
			if (opponent != null && opponent.InLobby && !m_OpponentJoined)
			{
				m_OpponentJoined = true;
				SlideInSide(m_OpponentSlideItems, m_OpponentSlideHomes);
			}

			ApplySide(me, m_SelfNameText, m_SelfNameBg, m_SelfStateText, m_SelfStateBg);
			ApplySide(opponent, m_OpponentNameText, m_OpponentNameBg, m_OpponentStateText, m_OpponentStateBg);

			ApplyLabels(me, opponent);
			ApplyGlobalState(me, opponent);
		}

		// 名札 → BG → 準備状態 の順に、間隔を空けて滑り込ませる。
		private void SlideInSide(RectTransform[] items, Vector3[] homes)
		{
			if (items == null) return;
			for (int i = 0; i < items.Length; i++)
			{
				if (items[i] == null) continue;
				items[i].DOLocalMove(homes[i], m_SlideDuration)
					.SetDelay(i * m_SlideStagger)
					.SetEase(m_SlideEase);
			}
		}

		// 1 サイド分の名前（白文字 + role 色の背景）と準備状態を反映する。
		private void ApplySide(PlayerDto dto, TMP_Text nameText, Image nameBg, TMP_Text stateText, Image stateBg)
		{
			if (nameText != null) nameText.color = Color.white;
			if (nameBg != null) nameBg.color = RoleColor(dto);

			bool ready = dto != null && dto.Ready;
			if (stateText != null) stateText.text = ready ? "Ready" : "Waiting";
			if (stateBg != null) stateBg.color = ready ? m_ReadyBgColor : m_WaitingBgColor;
		}

		private Color RoleColor(PlayerDto dto)
		{
			return dto != null && dto.Role == Roles.P2 ? m_P2Color : m_P1Color;
		}

		// 上部ラベル。左=自分・右=相手で固定。中身（P1/P2）と色は role に従う。
		private void ApplyLabels(PlayerDto me, PlayerDto opponent)
		{
			ApplyOneLabel(m_LeftLabel, me, m_SelfLabelSize);
			ApplyOneLabel(m_RightLabel, opponent, m_OpponentLabelSize);
		}

		private void ApplyOneLabel(TMP_Text label, PlayerDto dto, float size)
		{
			if (label == null) return;
			label.text = dto != null ? dto.Role : "";
			label.color = RoleColor(dto);
			label.fontSize = size;
		}

		// カウントダウン中は触らない。それ以外は Stand By を表示する。
		private void ApplyGlobalState(PlayerDto me, PlayerDto opponent)
		{
			if (m_CountdownCo != null) return;
			if (m_GlobalStateText != null) m_GlobalStateText.text = "Stand By";
		}

		// サーバが双方準備完了を通知。カウントダウンを開始し、完走したら Boot へ遷移する。
		private void HandleCountdownStart()
		{
			if (m_CountdownCo != null) StopCoroutine(m_CountdownCo);
			m_CountdownCo = StartCoroutine(CountdownSequence());
		}

		// カウントダウン中に誰かが準備を取り消した。中断して Stand By に戻す。
		private void HandleCountdownCancel()
		{
			StopCountdown();
			if (m_GlobalStateText != null) m_GlobalStateText.text = "Stand By";
		}

		private IEnumerator CountdownSequence()
		{
			for (int i = 3; i >= 1; i--)
			{
				if (m_GlobalStateText != null) m_GlobalStateText.text = i.ToString();
				yield return new WaitForSeconds(m_CountdownStep);
			}
			if (m_GlobalStateText != null) m_GlobalStateText.text = "GO!";
			yield return new WaitForSeconds(m_CountdownStep);
			m_CountdownCo = null;
			// カウントダウン完走後に遷移する。
			if (m_SceneLoader != null) m_SceneLoader.LoadScene("Boot");
		}

		private void StopCountdown()
		{
			if (m_CountdownCo != null)
			{
				StopCoroutine(m_CountdownCo);
				m_CountdownCo = null;
			}
		}

		private void WireCharaButtons()
		{
			if (m_CharaButtons != null)
			{
				for (int i = 0; i < m_CharaButtons.Length; i++)
				{
					// ボタン index 0/1/2（A/B/C）は立ち絵 index 1/2/3 に対応。
					int spriteIndex = i + 1;
					if (m_CharaButtons[i] != null)
						m_CharaButtons[i].onClick.AddListener(() => SelectChara(spriteIndex));
				}
			}
			// ランダムボタンは立ち絵 index 0。
			if (m_RandomButton != null)
				m_RandomButton.onClick.AddListener(() => SelectChara(0));
		}

		// キャラ選択。選択中ボタンを拡大し、自分側 Portrait の立ち絵を切り替える。
		// 引数は立ち絵 index：0=ランダム, 1=A, 2=B, 3=C。
		private void SelectChara(int spriteIndex)
		{
			// ランダム + A/B/C を 1 グループとして、選択中だけ拡大する。
			SetButtonScale(m_RandomButton, spriteIndex == 0);
			for (int i = 0; i < m_CharaButtons.Length; i++)
				SetButtonScale(m_CharaButtons[i], i + 1 == spriteIndex);

			SwapCharaSprite(spriteIndex);
		}

		private void SetButtonScale(Button button, bool selected)
		{
			if (button == null) return;
			float scale = selected ? m_CharaSelectedScale : 1f;
			button.transform.localScale = new Vector3(scale, scale, 1f);
		}

		// 自分側 Portrait の 2 枚スロットの初期状態を作る。活動スロットにランダム絵を置き、
		// 待機スロットは透明にしておく。スロットが無い（prefab 未改修）場合は何もしない。
		private void InitCharaSlots()
		{
			if (m_SelfPortraitSlotA == null || m_SelfPortraitSlotB == null) return;

			m_SelfActiveSlot = m_SelfPortraitSlotA;
			m_SelfIdleSlot = m_SelfPortraitSlotB;
			m_SelfSlotHomeX = m_SelfPortraitSlotA.rectTransform.localPosition.x;
			m_SelfCharaIndex = 0;

			SetSlotSprite(m_SelfActiveSlot, 0, 1f);
			SetSlotSprite(m_SelfIdleSlot, -1, 0f);

			// 初期はランダム選択中。ボタンの拡大表示も合わせる。
			SetButtonScale(m_RandomButton, true);
			for (int i = 0; i < m_CharaButtons.Length; i++)
				SetButtonScale(m_CharaButtons[i], false);
		}

		// 活動スロットの旧絵を外側（左）へ退場させつつ、待機スロットに新絵を入れて内側（右）から入場させる。
		private void SwapCharaSprite(int spriteIndex)
		{
			if (m_SelfActiveSlot == null || m_SelfIdleSlot == null) return;
			// 同じキャラの連打は無視する。
			if (spriteIndex == m_SelfCharaIndex) return;
			m_SelfCharaIndex = spriteIndex;

			var outgoing = m_SelfActiveSlot;
			var incoming = m_SelfIdleSlot;

			// 進行中のアニメを止めてから組み直す（連続切替で値が壊れないように）。
			outgoing.DOKill();
			incoming.DOKill();

			// 退場：左へずらしつつフェードアウト。
			MoveSlotX(outgoing, m_SelfSlotHomeX - m_CharaExitOffset);
			FadeSlot(outgoing, 0f);

			// 入場：新絵をセットし、右の開始位置・透明から定位置・不透明へ。
			SetSlotSprite(incoming, spriteIndex, 0f);
			incoming.rectTransform.localPosition =
				new Vector3(m_SelfSlotHomeX + m_CharaEnterOffset,
					incoming.rectTransform.localPosition.y,
					incoming.rectTransform.localPosition.z);
			MoveSlotX(incoming, m_SelfSlotHomeX);
			FadeSlot(incoming, 1f);

			// 役割を入れ替える。
			m_SelfActiveSlot = incoming;
			m_SelfIdleSlot = outgoing;
		}

		// スロットに立ち絵を設定し、alpha を即時反映する。
		// 対応する立ち絵が用意されていない場合は prefab に置いた仮絵をそのまま残し、
		// 位置とフェードのアニメだけ動かせるようにする（絵が揃う前でも動作確認できる）。
		private void SetSlotSprite(Image slot, int spriteIndex, float alpha)
		{
			if (slot == null) return;
			if (m_CharaSprites != null && spriteIndex >= 0 && spriteIndex < m_CharaSprites.Length
				&& m_CharaSprites[spriteIndex] != null)
				slot.sprite = m_CharaSprites[spriteIndex];
			var c = slot.color;
			slot.color = new Color(c.r, c.g, c.b, alpha);
		}

		private void MoveSlotX(Image slot, float targetX)
		{
			slot.rectTransform.DOLocalMoveX(targetX, m_CharaSwapDuration).SetEase(m_CharaSwapEase);
		}

		// DOTween 無料版で確実に使える DOTween.To で alpha をアニメする（Image.DOFade に依存しない）。
		private void FadeSlot(Image slot, float targetAlpha)
		{
			DOTween.To(() => slot.color.a, a =>
			{
				var c = slot.color;
				slot.color = new Color(c.r, c.g, c.b, a);
			}, targetAlpha, m_CharaSwapDuration).SetEase(m_CharaSwapEase);
		}

	}
}
