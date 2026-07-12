using System.Collections;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	// ファイナルレイズの提案 / 応答 UI。
	//
	// パネルは左右 2 つ（YouPanel / OpponentPanel）で構成され、
	// 自分と相手のどちらが操作中かをそれぞれの位置に表示する。
	// 提案フェーズ：敗者が「挑戦する / 降りる」を選ぶ。
	// 応答フェーズ：勝者が「受ける / 拒否」を選ぶ。
	// 拒否・タイムアウト・切断は全てサーバ側で game_over に流される。
	//
	// 【コントローラー対応（追加）】
	//   自分が操作側（m_IsActive）のときだけ反応する。
	//   左スティック左右 / 十字キー左右 … 2 ボタン間のカーソル移動
	//   B ボタン（buttonEast）          … 決定
	//   提案フェーズ：[ChallengeButton, FoldButton]
	//   応答フェーズ：[AcceptButton, RefuseButton]
	//
	// 子オブジェクトの参照は名前ベースで自動取得する。Prefab で以下の構造を保つこと：
	//   FinalRaisePanel (このスクリプト, 常駐 active)
	//     Root                       ← 表示切替対象。名前は "Root" 固定。無ければスクリプト自身を使う
	//       YouPanel
	//         Winner / AcceptButton, RefuseButton
	//         Loser  / ChallengeButton, FoldButton
	//         CountdownPanel / Text (TMP)
	//       OpponentPanel
	//         Winner / AcceptButton, RefuseButton
	//         Loser  / ChallengeButton, FoldButton
	//         CountdownPanel / Text (TMP)
	//       Status (TMP)             ← 任意
	public class FinalRaisePanelView : MonoBehaviour
	{
		// ─────────────────────────────────────────────────────────────
		// 定数（コントローラー UI 用）
		// ─────────────────────────────────────────────────────────────

		/// <summary>スティック入力のデッドゾーン（これ未満は無入力と見なす）</summary>
		private const float m_StickDeadZone = 0.3f;

		/// <summary>選択移動のクールダウン（秒）。連続入力チカチカ防止用</summary>
		private const float m_NavCooldown = 0.2f;

		// 表示切替対象。Awake で "Root" 子を探し、無ければ自分自身。
		private GameObject m_Root;
		// 自分側 / 相手側のセット。Awake で名前検索して埋める。
		private SideRefs m_You;
		private SideRefs m_Opp;
		private TMP_Text m_StatusText;

		private IGameState m_State;
		private Coroutine m_TimerCo;

		private enum EStage { Hidden, Offer, Pending }
		private EStage m_Stage = EStage.Hidden;

		private bool m_IsActive;
		private bool m_Submitted;

        private GameObject m_WaitingPanel;
        private TMP_Text m_WaitingText;

        // ─────────────────────────────────────────────────────────────
        // コントローラー UI 用フィールド（追加）
        // ─────────────────────────────────────────────────────────────

        /// <summary>左スティック + 十字キーを統合した Vector2 アクション</summary>
        private InputAction m_NavigateAction;

		/// <summary>B ボタン（buttonEast）による決定アクション</summary>
		private InputAction m_ConfirmUiAction;

		/// <summary>
		/// 現在のステージで操作対象になる 2 ボタン。
		/// 提案：[ChallengeButton, FoldButton] / 応答：[AcceptButton, RefuseButton]。
		/// 非アクティブ時は null。
		/// </summary>
		private Button[] m_ActiveButtons;

		/// <summary>現在フォーカスしているボタンのインデックス</summary>
		private int m_SelectedIndex;

		/// <summary>選択移動のクールダウン残り時間（秒）</summary>
		private float m_NavCooldownRemaining;

		private class SideRefs
		{
			public GameObject WinnerGroup;
			public GameObject LoserGroup;
			public Button     AcceptButton;
			public Button     RefuseButton;
			public Button     ChallengeButton;
			public Button     FoldButton;
			public GameObject CountdownPanel;
			public TMP_Text   CountdownText;
		}

		private void Awake()
		{
			var rootTr = transform.Find("Root");
			m_Root = rootTr != null ? rootTr.gameObject : gameObject;

			m_You = ResolveSide("YouPanel");
			m_Opp = ResolveSide("OpponentPanel");
			m_StatusText = FindChild<TMP_Text>(m_Root.transform, "Status");
			var waitingTr = FindChild<Transform>(m_Root.transform, "WaitinPanel");
			m_WaitingPanel = waitingTr != null ? waitingTr.gameObject : null;
			m_WaitingText = m_WaitingPanel != null
				? FindChild<TMP_Text>(m_WaitingPanel.transform, "WaitingMessageText")
				: null;

            HideWaiting();
            SetActiveSafe(m_Root, false);

			BindClick(m_You.ChallengeButton, () => SubmitProposeIfAllowed(true));
			BindClick(m_You.FoldButton,      () => SubmitProposeIfAllowed(false));
			BindClick(m_You.AcceptButton,    () => SubmitRespondIfAllowed(true));
			BindClick(m_You.RefuseButton,    () => SubmitRespondIfAllowed(false));

			// コントローラー入力を構築する
			BuildUiActions();
			RegisterUiCallbacks();
		}

		private SideRefs ResolveSide(string sideName)
		{
			var side = m_Root.transform.Find(sideName);
			if (side == null)
			{
				Debug.LogError($"[FinalRaisePanel] '{sideName}' が見つかりません");
				return new SideRefs();
			}

			var winner = side.Find("Winner");
			var loser  = side.Find("Loser");
			var cdPanel = side.Find("CountdownPanel");

			return new SideRefs
			{
				WinnerGroup     = winner != null ? winner.gameObject : null,
				LoserGroup      = loser  != null ? loser.gameObject  : null,
				AcceptButton    = winner != null ? FindChild<Button>(winner, "AcceptButton")    : null,
				RefuseButton    = winner != null ? FindChild<Button>(winner, "RefuseButton")    : null,
				ChallengeButton = loser  != null ? FindChild<Button>(loser,  "ChallengeButton") : null,
				FoldButton      = loser  != null ? FindChild<Button>(loser,  "FoldButton")      : null,
				CountdownPanel  = cdPanel != null ? cdPanel.gameObject : null,
				CountdownText   = cdPanel != null ? cdPanel.GetComponentInChildren<TMP_Text>(true) : null
			};
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

			EnableUiActions();
		}

		private void OnDestroy()
		{
			if (m_State != null)
			{
				m_State.OnFinalRaiseOffer     -= HandleOffer;
				m_State.OnFinalRaisePending   -= HandlePending;
				m_State.OnFinalRaiseCanceled  -= HandleCanceled;
				m_State.OnFinalRaiseStarted   -= HandleStarted;
				m_State.OnGameOver            -= HandleGameOver;
			}

			DisableUiActions();
			DisposeUiActions();
		}

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 入力：InputAction 構築
		// ─────────────────────────────────────────────────────────────

		private void BuildUiActions()
		{
			// 左スティックと十字キーの両方を 1 つの Vector2 Action に統合する
			m_NavigateAction = new InputAction("FRNavigate", InputActionType.Value, expectedControlType: "Vector2");
			m_NavigateAction.AddBinding("<Gamepad>/leftStick");
			m_NavigateAction.AddCompositeBinding("Dpad")
				.With("Up",    "<Gamepad>/dpad/up")
				.With("Down",  "<Gamepad>/dpad/down")
				.With("Left",  "<Gamepad>/dpad/left")
				.With("Right", "<Gamepad>/dpad/right");

			// B ボタン（buttonEast）で決定する
			// Xbox = B / Switch Pro = A（右側ボタン）
			m_ConfirmUiAction = new InputAction("FRConfirm", InputActionType.Button);
			m_ConfirmUiAction.AddBinding("<Gamepad>/buttonEast");
		}

		private void RegisterUiCallbacks()
		{
			m_ConfirmUiAction.performed += _ => OnConfirmUi();
		}

		private void EnableUiActions()
		{
			m_NavigateAction.Enable();
			m_ConfirmUiAction.Enable();
		}

		private void DisableUiActions()
		{
			m_NavigateAction?.Disable();
			m_ConfirmUiAction?.Disable();
		}

		private void DisposeUiActions()
		{
			m_NavigateAction?.Dispose();
			m_ConfirmUiAction?.Dispose();
		}

		// ─────────────────────────────────────────────────────────────
		// コントローラー UI 入力：毎フレーム処理
		// ─────────────────────────────────────────────────────────────

		private void Update()
		{
			// 自分が操作側でない、または送信済み・非表示なら入力を受け付けない
			if (!m_IsActive || m_Submitted || m_Stage == EStage.Hidden) return;
			if (m_ActiveButtons == null) return;

			m_NavCooldownRemaining -= Time.deltaTime;

			float axisX = m_NavigateAction.ReadValue<Vector2>().x;
			HandleNavigation(axisX);
		}

		/// <summary>
		/// 左スティック左右 / 十字キー左右で 2 ボタン間のカーソルを移動する。
		/// interactable なボタンのみを対象とし、クールダウンでチカチカを防止する。
		/// </summary>
		private void HandleNavigation(float axisX)
		{
			if (m_NavCooldownRemaining > 0f) return;
			if (Mathf.Abs(axisX) < m_StickDeadZone) return;

			int direction = axisX > 0 ? 1 : -1;
			int next = FindNextInteractable(m_ActiveButtons, m_SelectedIndex, direction);
			if (next == m_SelectedIndex) return;

			m_SelectedIndex        = next;
			m_NavCooldownRemaining = m_NavCooldown;
			FocusButton(m_ActiveButtons, m_SelectedIndex);
		}

		/// <summary>
		/// B ボタン押下時に現在フォーカス中のボタンを押す。
		/// 自分が操作側でない、または送信済みなら無視。
		/// </summary>
		private void OnConfirmUi()
		{
			if (!m_IsActive || m_Submitted || m_Stage == EStage.Hidden) return;
			if (m_ActiveButtons == null) return;
			if (m_SelectedIndex < 0 || m_SelectedIndex >= m_ActiveButtons.Length) return;

			var button = m_ActiveButtons[m_SelectedIndex];
			if (button == null || !button.interactable) return;
			button.onClick.Invoke();
		}

		/// <summary>
		/// 指定方向で次に interactable なボタンのインデックスを返す。
		/// 端で見つからなければ現在のインデックスを返す（移動しない）。
		/// </summary>
		private static int FindNextInteractable(Button[] buttons, int current, int direction)
		{
			int i = current + direction;
			while (i >= 0 && i < buttons.Length)
			{
				if (buttons[i] != null && buttons[i].interactable) return i;
				i += direction;
			}
			return current;
		}

		/// <summary>
		/// 配列の中で最初に interactable なインデックスを返す。見つからなければ 0。
		/// </summary>
		private static int FindFirstInteractable(Button[] buttons)
		{
			if (buttons == null) return 0;
			for (int i = 0; i < buttons.Length; i++)
				if (buttons[i] != null && buttons[i].interactable) return i;
			return 0;
		}

		/// <summary>
		/// 指定ボタンに EventSystem のフォーカスを移す。
		/// Button の選択時ハイライト（や ButtonFocusHighlight）が自動で適用される。
		/// </summary>
		private static void FocusButton(Button[] buttons, int index)
		{
			if (buttons == null) return;
			if (index < 0 || index >= buttons.Length) return;

			var button = buttons[index];
			if (button == null) return;

			if (EventSystem.current != null)
				EventSystem.current.SetSelectedGameObject(button.gameObject);
		}

		/// <summary>
		/// 現在のステージに応じて操作対象ボタン配列を構築し、先頭にフォーカスを移す。
		/// 自分が操作側でない場合は配列を null にして入力を無効化する。
		/// </summary>
		private void SetupControllerFocus()
		{
			if (!m_IsActive)
			{
				m_ActiveButtons = null;
				return;
			}

			m_ActiveButtons = m_Stage == EStage.Offer
				? new[] { m_You.ChallengeButton, m_You.FoldButton }   // 提案フェーズ
				: new[] { m_You.AcceptButton,    m_You.RefuseButton }; // 応答フェーズ

			m_NavCooldownRemaining = 0f;
			m_SelectedIndex        = FindFirstInteractable(m_ActiveButtons);
			FocusButton(m_ActiveButtons, m_SelectedIndex);
		}

		// ─────────────────────────────────────────────────────────────
		// 既存メソッド群（変更なし。コントローラーフォーカス設定のみ追記）
		// ─────────────────────────────────────────────────────────────

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
			m_Submitted = false;

			string myRole = m_State.Me != null ? m_State.Me.Role : null;
			bool meIsProposer  = myRole != null && myRole == proposerRole;
			bool meIsResponder = myRole != null && myRole == responderRole;

			// 提案フェーズの操作側は敗者、応答フェーズの操作側は勝者。
			m_IsActive = stage == EStage.Offer ? meIsProposer : meIsResponder;

			if (m_IsActive)
			{
				HideWaiting();
			}
			else
			{
				ShowWaiting("対戦相手を待っています...");
			}

			// 自分が操作側のときだけ自分側にボタンを出す。相手側のボタンは常に非表示。
            // （相手の選択肢は知る必要がない。「相手が選択中」というのは CountdownPanel の存在で伝える）
            bool youShowLoser  = m_IsActive && stage == EStage.Offer;
			bool youShowWinner = m_IsActive && stage == EStage.Pending;

			SetActiveSafe(m_You.LoserGroup,  youShowLoser);
			SetActiveSafe(m_You.WinnerGroup, youShowWinner);
			SetActiveSafe(m_Opp.LoserGroup,  false);
			SetActiveSafe(m_Opp.WinnerGroup, false);

			SetInteractable(m_You.ChallengeButton, m_IsActive);
			SetInteractable(m_You.FoldButton,      m_IsActive);
			SetInteractable(m_You.AcceptButton,    m_IsActive);
			SetInteractable(m_You.RefuseButton,    m_IsActive);

			// カウントダウンは自分が操作側のときだけ自分側に表示する。
			// 相手の残り時間は隠す（相手の決断状況を知らせない）。
			SetActiveSafe(m_You.CountdownPanel, m_IsActive);
			SetActiveSafe(m_Opp.CountdownPanel, false);

			SetStatus("");
			SetActiveSafe(m_Root, true);
			RestartTimer(timeoutMs);

			// コントローラー：操作対象ボタンを設定して先頭にフォーカスする
			SetupControllerFocus();
		}

		private void Hide()
		{
			m_Stage = EStage.Hidden;
			m_IsActive = false;
			m_Submitted = false;
			StopTimer();

			SetActiveSafe(m_You.LoserGroup,     false);
			SetActiveSafe(m_You.WinnerGroup,    false);
			SetActiveSafe(m_Opp.LoserGroup,     false);
			SetActiveSafe(m_Opp.WinnerGroup,    false);
			SetActiveSafe(m_You.CountdownPanel, false);
			SetActiveSafe(m_Opp.CountdownPanel, false);
			SetActiveSafe(m_Root, false);

			// コントローラー：操作対象をクリアして入力を無効化する
			m_ActiveButtons = null;
		}

		private void SubmitProposeIfAllowed(bool accept)
		{
			if (!m_IsActive || m_Submitted || m_Stage != EStage.Offer) return;
			m_Submitted = true;
			m_State.SubmitFinalRaisePropose(accept);
			LockAfterSubmit(accept ? "挑戦を申し込みました" : "降りました");
		}

		private void SubmitRespondIfAllowed(bool accept)
		{
			if (!m_IsActive || m_Submitted || m_Stage != EStage.Pending) return;
			m_Submitted = true;
			m_State.SubmitFinalRaiseRespond(accept);
			LockAfterSubmit(accept ? "ファイナルレイズ開始..." : "拒否しました");
		}

		// 送信直後のロック表示。
		// 「挑戦」を選んだ場合は応答フェーズ通知を待つので、ここでは自分側ボタンを操作不可にしてステータスを出すだけ。
		// 拒否・取り消し・本番開始時の Panel 非表示は HandleCanceled / HandleStarted / HandleGameOver が担当する。
		private void LockAfterSubmit(string status)
		{
			SetInteractable(m_You.ChallengeButton, false);
			SetInteractable(m_You.FoldButton,      false);
			SetInteractable(m_You.AcceptButton,    false);
			SetInteractable(m_You.RefuseButton,    false);
			SetStatus(status);

			// コントローラー：送信後は操作対象をクリアして再入力を防ぐ
			m_ActiveButtons = null;
		}

		private void SetStatus(string text)
		{
			if (m_StatusText == null) return;
			m_StatusText.text = text;
			SetActiveSafe(m_StatusText.gameObject, !string.IsNullOrEmpty(text));
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
			SetCountdownText("");
		}

		private IEnumerator TimerSequence(float seconds)
		{
			float remaining = seconds;
			while (remaining > 0f)
			{
				SetCountdownText(Mathf.CeilToInt(remaining) + "s");
				remaining -= Time.deltaTime;
				yield return null;
			}
			SetCountdownText("0s");
			m_TimerCo = null;
		}

		private void SetCountdownText(string text)
		{
			if (m_You.CountdownText != null) m_You.CountdownText.text = m_IsActive  ? text : "";
			if (m_Opp.CountdownText != null) m_Opp.CountdownText.text = !m_IsActive ? text : "";
		}

		private static void BindClick(Button button, System.Action handler)
		{
			if (button == null) return;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(new UnityEngine.Events.UnityAction(handler));
		}

		private static void SetInteractable(Button button, bool value)
		{
			if (button != null) button.interactable = value;
		}

		private static void SetActiveSafe(GameObject go, bool active)
		{
			if (go != null && go.activeSelf != active) go.SetActive(active);
		}

        private void ShowWaiting(string text)
        {
            if (m_WaitingText != null)
                m_WaitingText.text = text;

            SetActiveSafe(m_WaitingPanel, true);
        }

        private void HideWaiting()
        {
            SetActiveSafe(m_WaitingPanel, false);
        }
        
		// 直下の子から名前一致を探し、見つからなければ非アクティブも含めて再帰検索する。
        private static T FindChild<T>(Transform root, string name) where T : Component
		{
			if (root == null) return null;
			var direct = root.Find(name);
			if (direct != null)
			{
				var c = direct.GetComponent<T>();
				if (c != null) return c;
			}
			var all = root.GetComponentsInChildren<T>(true);
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i].name == name) return all[i];
			}
			return null;
		}
	}
}