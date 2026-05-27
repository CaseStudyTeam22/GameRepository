using System.Collections;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	// ファイナルレイズの提案 / 応答 UI。
	//
	// 双方同じ Panel を見るが、操作できるのはその時点で手番のプレイヤーだけ。
	// 確定ボタン（FIGHT / ACCEPT）と拒否ボタン（FOLD / DECLINE）の 2 つを長押し 2 秒で確定。
	// ボタン背景の Image.fillAmount を 0→1 にして充塔フィードバックを出す。
	// 確定後はその局面の入力をロックする。一度発起したら撤回できない。
	public class FinalRaisePanelView : MonoBehaviour
	{
		[Header("Root")]
		[Tooltip("Panel 全体の表示切替に使うルート GameObject")]
		[SerializeField] private GameObject m_Root;

		[Header("Header")]
		[Tooltip("タイトル表示。フェーズに応じて文言が変わる")]
		[SerializeField] private TMP_Text m_TitleText;
		[Tooltip("残り時間表示")]
		[SerializeField] private TMP_Text m_TimerText;

		[Header("Buttons")]
		[Tooltip("確定側ボタン（提案では FIGHT、応答では ACCEPT）。長押し 2 秒で確定。")]
		[SerializeField] private Button m_ConfirmButton;
		[Tooltip("確定側ボタンのラベル")]
		[SerializeField] private TMP_Text m_ConfirmLabel;
		[Tooltip("確定側ボタンの充塔表示用 Image（Image Type = Filled）")]
		[SerializeField] private Image m_ConfirmFill;

		[Tooltip("拒否側ボタン（提案では FOLD、応答では DECLINE）。長押し 2 秒で確定。")]
		[SerializeField] private Button m_DeclineButton;
		[Tooltip("拒否側ボタンのラベル")]
		[SerializeField] private TMP_Text m_DeclineLabel;
		[Tooltip("拒否側ボタンの充塔表示用 Image（Image Type = Filled）")]
		[SerializeField] private Image m_DeclineFill;

		[Header("Status")]
		[Tooltip("待機中などの状況テキスト。操作可能側には空表示")]
		[SerializeField] private TMP_Text m_StatusText;

		[Header("Tuning")]
		[Tooltip("ボタン長押しで確定するまでの保持秒数")]
		[SerializeField] private float m_HoldSeconds = 2f;

		private IGameState m_State;
		private Coroutine m_TimerCo;

		// 現在の局面（提案 / 応答 / 非表示）。
		private enum EStage { Hidden, Offer, Pending }
		private EStage m_Stage = EStage.Hidden;

		// どちらのボタンを押下中か。
		private enum EHeld { None, Confirm, Decline }
		private EHeld m_Held = EHeld.None;
		// 押下開始からの経過秒数。
		private float m_HoldElapsed;

		// 操作可能側（true なら自分の手番）。
		private bool m_IsActive;
		// 確定済かどうか。確定後は再操作不可。
		private bool m_Locked;

		private void Awake()
		{
			if (m_Root == null) m_Root = gameObject;
			SetActiveSafe(m_Root, false);

			BindHoldEvents(m_ConfirmButton, EHeld.Confirm);
			BindHoldEvents(m_DeclineButton, EHeld.Decline);

			// TODO: ゲームパッド対応（決定ボタン / キャンセルボタンの長押しで Confirm / Decline と同じ動作を駆動）。
		}

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[FinalRaisePanel] GameStateLocator.Current is null");
				return;
			}

			m_State.OnFinalRaiseOffer     += HandleOffer;
			m_State.OnFinalRaisePending   += HandlePending;
			m_State.OnFinalRaiseCanceled  += HandleCanceled;
			m_State.OnFinalRaiseStarted   += HandleStarted;
			m_State.OnGameOver            += HandleGameOver;
		}

		private void OnDestroy()
		{
			if (m_State == null) return;
			m_State.OnFinalRaiseOffer     -= HandleOffer;
			m_State.OnFinalRaisePending   -= HandlePending;
			m_State.OnFinalRaiseCanceled  -= HandleCanceled;
			m_State.OnFinalRaiseStarted   -= HandleStarted;
			m_State.OnGameOver            -= HandleGameOver;
		}

		private void Update()
		{
			if (m_Stage == EStage.Hidden) return;

			if (!m_IsActive || m_Locked)
			{
				if (m_Held != EHeld.None)
				{
					m_Held = EHeld.None;
					m_HoldElapsed = 0f;
				}
				UpdateFill(EHeld.None, 0f);
				return;
			}

			if (m_Held == EHeld.None)
			{
				m_HoldElapsed = 0f;
				UpdateFill(EHeld.None, 0f);
				return;
			}

			m_HoldElapsed += Time.deltaTime;
			float ratio = Mathf.Clamp01(m_HoldElapsed / m_HoldSeconds);
			UpdateFill(m_Held, ratio);

			if (m_HoldElapsed >= m_HoldSeconds)
			{
				Confirm(m_Held == EHeld.Confirm);
			}
		}

		private void HandleOffer(FinalRaiseOfferMessage msg)
		{
			BeginStage(EStage.Offer, msg.ProposerRole, msg.ResponderRole, msg.TimeoutMs);
		}

		private void HandlePending(FinalRaisePendingMessage msg)
		{
			BeginStage(EStage.Pending, msg.ProposerRole, msg.ResponderRole, msg.TimeoutMs);
		}

		private void HandleCanceled(FinalRaiseCanceledMessage msg)
		{
			Hide();
		}

		private void HandleStarted()
		{
			Hide();
		}

		private void HandleGameOver(string winnerRole)
		{
			// 何らかの理由で Hide し損ねていた場合の保険。
			Hide();
		}

		private void BeginStage(EStage stage, string proposerRole, string responderRole, int timeoutMs)
		{
			m_Stage = stage;
			m_Locked = false;
			m_Held = EHeld.None;
			m_HoldElapsed = 0f;

			string myRole = m_State.Me != null ? m_State.Me.Role : null;
			bool meIsProposer = myRole != null && myRole == proposerRole;
			bool meIsResponder = myRole != null && myRole == responderRole;

			if (stage == EStage.Offer)
			{
				m_IsActive = meIsProposer;
				SetLabels("FIGHT", "FOLD");
				SetTitle("FINAL RAISE?");
				SetStatus(m_IsActive ? "" : "WAITING FOR LOSER...");
			}
			else
			{
				m_IsActive = meIsResponder;
				SetLabels("ACCEPT", "DECLINE");
				SetTitle("FINAL RAISE?");
				SetStatus(m_IsActive ? "" : "WAITING FOR OPPONENT...");
			}

			SetButtonsInteractable(m_IsActive);
			UpdateFill(EHeld.None, 0f);
			SetActiveSafe(m_Root, true);
			RestartTimer(timeoutMs);
		}

		private void Hide()
		{
			m_Stage = EStage.Hidden;
			m_IsActive = false;
			m_Locked = false;
			m_Held = EHeld.None;
			m_HoldElapsed = 0f;
			StopTimer();
			UpdateFill(EHeld.None, 0f);
			SetActiveSafe(m_Root, false);
		}

		// 長押し 2 秒経過時の確定処理。
		private void Confirm(bool accept)
		{
			if (m_State == null || m_Locked) return;

			m_Locked = true;
			m_Held = EHeld.None;

			if (m_Stage == EStage.Offer)
				m_State.SubmitFinalRaisePropose(accept);
			else if (m_Stage == EStage.Pending)
				m_State.SubmitFinalRaiseRespond(accept);

			UpdateFill(accept ? EHeld.Confirm : EHeld.Decline, 1f);
			SetButtonsInteractable(false);
			SetStatus(accept ? "LOCKED IN" : "FOLDED");
		}

		private void UpdateFill(EHeld held, float ratio)
		{
			if (m_ConfirmFill != null) m_ConfirmFill.fillAmount = held == EHeld.Confirm ? ratio : 0f;
			if (m_DeclineFill != null) m_DeclineFill.fillAmount = held == EHeld.Decline ? ratio : 0f;
		}

		private void SetLabels(string confirm, string decline)
		{
			if (m_ConfirmLabel != null) m_ConfirmLabel.text = confirm;
			if (m_DeclineLabel != null) m_DeclineLabel.text = decline;
		}

		private void SetTitle(string text)
		{
			if (m_TitleText != null) m_TitleText.text = text;
		}

		private void SetStatus(string text)
		{
			if (m_StatusText != null) m_StatusText.text = text;
		}

		private void SetButtonsInteractable(bool value)
		{
			if (m_ConfirmButton != null) m_ConfirmButton.interactable = value;
			if (m_DeclineButton != null) m_DeclineButton.interactable = value;
		}

		private void RestartTimer(int timeoutMs)
		{
			StopTimer();
			m_TimerCo = StartCoroutine(TimerSequence(timeoutMs / 1000f));
		}

		private void StopTimer()
		{
			if (m_TimerCo != null)
			{
				StopCoroutine(m_TimerCo);
				m_TimerCo = null;
			}
			if (m_TimerText != null) m_TimerText.text = "";
		}

		private IEnumerator TimerSequence(float seconds)
		{
			float remaining = seconds;
			while (remaining > 0f)
			{
				if (m_TimerText != null) m_TimerText.text = Mathf.CeilToInt(remaining).ToString();
				remaining -= Time.deltaTime;
				yield return null;
			}
			if (m_TimerText != null) m_TimerText.text = "0";
			m_TimerCo = null;
		}

		// ボタンに PointerDown / PointerUp / PointerExit を仕込み、押している間だけ m_Held をセット。
		private void BindHoldEvents(Button button, EHeld held)
		{
			if (button == null) return;

			var trigger = button.gameObject.GetComponent<EventTrigger>();
			if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

			AddTrigger(trigger, EventTriggerType.PointerDown, _ =>
			{
				if (!m_IsActive || m_Locked) return;
				if (!button.interactable) return;
				m_Held = held;
				m_HoldElapsed = 0f;
			});
			AddTrigger(trigger, EventTriggerType.PointerUp, _ =>
			{
				if (m_Held == held)
				{
					m_Held = EHeld.None;
					m_HoldElapsed = 0f;
				}
			});
			AddTrigger(trigger, EventTriggerType.PointerExit, _ =>
			{
				// ボタン外へ指/カーソルが出たらキャンセル扱い。
				if (m_Held == held)
				{
					m_Held = EHeld.None;
					m_HoldElapsed = 0f;
				}
			});
		}

		private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> cb)
		{
			var entry = new EventTrigger.Entry { eventID = type };
			entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(cb));
			trigger.triggers.Add(entry);
		}

		private static void SetActiveSafe(GameObject go, bool active)
		{
			if (go != null && go.activeSelf != active) go.SetActive(active);
		}
	}
}
