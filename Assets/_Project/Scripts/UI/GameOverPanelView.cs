using System.Collections;
using GamblingAction.Domain;
using TMPro;
using UnityEngine;

namespace GamblingAction.UI
{
    public class GameOverPanelView : MonoBehaviour
    {
        // ResultScene への自動遷移（FlowPanelView 側の 5 秒待ち）に表示を合わせる。
        private const float k_CountdownSeconds = 5f;

        [SerializeField] private TMP_Text m_ResultText;
        [SerializeField] private TMP_Text m_DetailText;
        [SerializeField] private TMP_Text m_CountdownText;

        private IGameState m_State;
        private Coroutine m_CountdownCo;

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
        }

        private void OnDisable()
        {
            if (m_State == null) return;
            m_State.OnGameOver -= HandleGameOver;
            StopCountdown();
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

            StartCountdown();
        }

        private void StartCountdown()
        {
            StopCountdown();
            if (m_CountdownText != null)
                m_CountdownCo = StartCoroutine(CountdownRoutine());
        }

        private void StopCountdown()
        {
            if (m_CountdownCo != null)
            {
                StopCoroutine(m_CountdownCo);
                m_CountdownCo = null;
            }
        }

        private IEnumerator CountdownRoutine()
        {
            int remaining = Mathf.CeilToInt(k_CountdownSeconds);
            while (remaining > 0)
            {
                m_CountdownText.text = remaining.ToString();
                yield return new WaitForSeconds(1f);
                remaining--;
            }
            m_CountdownText.text = "0";
        }
    }
}
