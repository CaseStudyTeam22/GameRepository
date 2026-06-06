using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
    public class GameOverPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_ResultText;
        [SerializeField] private TMP_Text m_DetailText;
        [SerializeField] private Button   m_PlayAgainButton;

        private IGameState m_State;

        private void Awake()
        {
            m_State = GameStateLocator.Current;
            if (m_State == null)
                Debug.LogError("[GameOverPanel] GameStateLocator.Current is null");
        }

        private void OnEnable()
        {
            if (m_State == null) return;
            m_State.OnGameOver += HandleGameOver;
            if (m_PlayAgainButton != null)
                m_PlayAgainButton.onClick.AddListener(() => m_State.RequestLobby());
        }

        private void OnDisable()
        {
            if (m_State == null) return;
            m_State.OnGameOver -= HandleGameOver;
            if (m_PlayAgainButton != null)
                m_PlayAgainButton.onClick.RemoveAllListeners();
        }

        private void HandleGameOver(string winnerRole)
        {
            bool iWon = m_State.Me != null && m_State.Me.Role == winnerRole;

            if (m_ResultText != null)
                m_ResultText.text = iWon ? "YOU WIN!" : "YOU LOSE...";

            if (m_DetailText != null && m_State.Me != null)
            {
                int myMoney  = m_State.Me.Money;
                int oppMoney = m_State.Opponent != null ? m_State.Opponent.Money : 0;
                m_DetailText.text = $"You: {myMoney:N0}  |  Opponent: {oppMoney:N0}  |  Winner: {winnerRole}";
            }
        }
    }
}
