using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	// Lobby シーンの準備処理。準備ボタンで SubmitReady を送り、
	// サーバが Lobby フェーズを抜けたら Boot シーンへ遷移する。
	public class LobbyController : MonoBehaviour
	{
		[SerializeField, Tooltip("準備ボタン")]
		private Button m_ReadyButton;
		[SerializeField, Tooltip("AI に任せるトグル。null 可")]
		private Toggle m_ReadyAsAIToggle;
		[SerializeField, Tooltip("待機状況の表示。null 可")]
		private TMP_Text m_StatusText;
		[SerializeField, Tooltip("Boot へ遷移する SceneLoader")]
		private SceneLoader m_SceneLoader;

		private IGameState m_State;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[Lobby] GameStateLocator.Current is null");
				return;
			}

			if (m_ReadyButton != null)
				m_ReadyButton.onClick.AddListener(OnReadyClicked);

			m_State.OnPhaseChanged     += HandlePhase;
			m_State.OnWaitingForOthers += HandleWaiting;
		}

		private void OnDestroy()
		{
			if (m_State == null) return;
			m_State.OnPhaseChanged     -= HandlePhase;
			m_State.OnWaitingForOthers -= HandleWaiting;
		}

		private void OnReadyClicked()
		{
			bool isAI = m_ReadyAsAIToggle != null && m_ReadyAsAIToggle.isOn;
			m_State.SubmitReady(isAI);
			if (m_ReadyButton != null) m_ReadyButton.interactable = false;
		}

		private void HandlePhase(EGamePhase phase)
		{
			// サーバが Lobby を抜けた = ゲーム開始。Boot へ遷移する。
			if (phase != EGamePhase.Lobby && m_SceneLoader != null)
				m_SceneLoader.LoadScene("Boot");
		}

		private void HandleWaiting(string message)
		{
			if (m_StatusText != null) m_StatusText.text = message;
		}
	}
}
