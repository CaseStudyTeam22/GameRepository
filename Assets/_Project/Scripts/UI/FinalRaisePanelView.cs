using System.Collections;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using TMPro;
using UnityEngine;
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

			SetActiveSafe(m_Root, false);

			BindClick(m_You.ChallengeButton, () => SubmitProposeIfAllowed(true));
			BindClick(m_You.FoldButton,      () => SubmitProposeIfAllowed(false));
			BindClick(m_You.AcceptButton,    () => SubmitRespondIfAllowed(true));
			BindClick(m_You.RefuseButton,    () => SubmitRespondIfAllowed(false));
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
