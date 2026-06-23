using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.UI
{
    public class ScoreManager : MonoBehaviour
    {
        [Header("Score Texts")]
        [SerializeField] private TMP_Text m_P1ScoreText;
        [SerializeField] private TMP_Text m_P2ScoreText;

        [Header("Player Roles")]
        [SerializeField] private string m_P1Role = "P1";
        [SerializeField] private string m_P2Role = "P2";

        [SerializeField] private string m_MissingPlayerText = "-";

        [Header("Score Positions")]
        [SerializeField] private Vector2 m_LeftPosition;
        [SerializeField] private Vector2 m_RightPosition;

        [Header("Sudden Death UI Prefab")]
        [SerializeField] private GameObject suddenDeathUIPrefab;   // Åö í«â¡

        private IGameState m_State;
        private bool m_SuddenDeathTriggered = false;

        private void Start()
        {
            Debug.Log("[ScoreManager] Start called. m_State=" + m_State);
            Debug.Log("[ScoreManager] Using GameState instance: " + m_State.GetHashCode());

            m_State = GameStateLocator.Current;
            if (m_State == null)
            {
                Debug.LogError("[ScoreManager] GameStateLocator.Current is null");
                return;
            }

            m_State.OnStateInitialized += RefreshScores;
            m_State.OnPlayersChanged += RefreshScores;

            
            m_State.OnSuddenDeathStarted += OnSuddenDeathStarted;

            if (m_State.SuddenDeathAlreadyStarted)
            {
                OnSuddenDeathStarted();
            }

            RefreshScores();
        }

        private void OnDestroy()
        {
            if (m_State == null) return;

            m_State.OnStateInitialized -= RefreshScores;
            m_State.OnPlayersChanged -= RefreshScores;
            m_State.OnSuddenDeathStarted -= OnSuddenDeathStarted; // Åö í«â¡
        }

        private void RefreshScores()
        {
            SetScoreText(m_P1ScoreText, TryGetScore(m_P1Role));
            SetScoreText(m_P2ScoreText, TryGetScore(m_P2Role));

            UpdatePosition();
            CheckSuddenDeath();
        }

        private void CheckSuddenDeath()
        {
            if (m_SuddenDeathTriggered)
                return;

            var p1 = TryGetScore(m_P1Role);
            var p2 = TryGetScore(m_P2Role);

            if (p1 == 2 && p2 == 2)
            {
                m_SuddenDeathTriggered = true;

                Debug.Log("[ScoreManager] Sudden Death Triggered!");

                m_State?.NotifySuddenDeathRequested();
            }
        }

        private void OnSuddenDeathStarted()
        {
            Debug.Log("[ScoreManager] Sudden Death UI Triggered");

            if (suddenDeathUIPrefab == null)
            {
                Debug.LogError("[ScoreManager] suddenDeathUIPrefab Ç™ê›íËÇ≥ÇÍÇƒÇ¢Ç‹ÇπÇÒ");
                return;
            }

            // Åö Canvas ÇÃâ∫Ç…ê∂ê¨
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ScoreManager] Canvas Ç™å©Ç¬Ç©ÇËÇ‹ÇπÇÒ");
                return;
            }

            Instantiate(suddenDeathUIPrefab, canvas.transform);
        }

        private int? TryGetScore(string role)
        {
            if (m_State == null || m_State.Players == null) return null;

            foreach (var player in m_State.Players.Values)
            {
                if (player == null) continue;
                if (player.Role != role) continue;

                return player.Score;
            }

            return null;
        }

        private void SetScoreText(TMP_Text text, int? score)
        {
            if (text == null) return;
            text.text = score.HasValue ? score.Value.ToString() : m_MissingPlayerText;
        }

        private void UpdatePosition()
        {
            if (m_State?.Me == null)
                return;

            var p1Rect = m_P1ScoreText?.GetComponent<RectTransform>();
            var p2Rect = m_P2ScoreText?.GetComponent<RectTransform>();

            if (m_State.Me.Role == m_P1Role)
            {
                p1Rect.anchoredPosition = m_LeftPosition;
                p2Rect.anchoredPosition = m_RightPosition;
            }
            else
            {
                p1Rect.anchoredPosition = m_RightPosition;
                p2Rect.anchoredPosition = m_LeftPosition;
            }
        }
    }
}
