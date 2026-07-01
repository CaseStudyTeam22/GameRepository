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

        [SerializeField] private TMP_Text m_DeathText;
        [SerializeField] private TMP_Text m_SuddenDeathTextTop;
        [SerializeField] private TMP_Text m_SuddenDeathTextBottom;
        [SerializeField] private GameObject m_SuddenDeathPanelTop;
        [SerializeField] private GameObject m_SuddenDeathPanelBottom;

        [SerializeField] private float m_ScrollSpeed = 100f;

        private bool m_IsSuddenDeathScrolling = false;
        private RectTransform m_TopRect;
        private RectTransform m_BottomRect;
        private float m_ScreenWidth;

        private IGameState m_State;
        private bool m_SuddenDeathTriggered = false;

        private void Start()
        {
            if (m_SuddenDeathTextTop != null)
                m_TopRect = m_SuddenDeathTextTop.GetComponent<RectTransform>();
            if (m_SuddenDeathTextBottom != null)
                m_BottomRect = m_SuddenDeathTextBottom.GetComponent<RectTransform>();

            m_ScreenWidth = Screen.width;

            m_State = GameStateLocator.Current;
            Debug.Log("[ScoreManager] Start called. m_State=" + m_State);
            if (m_State != null)
            {
                Debug.Log("[ScoreManager] Using GameState instance: " + m_State.GetHashCode());
            }
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
            m_State.OnSuddenDeathStarted -= OnSuddenDeathStarted; // ★ 追加
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
<<<<<<< HEAD

=======
                m_State.NotifySuddenDeathRequested();
>>>>>>> 5c32e2b86f3b5fe180939fce787e752346239ce3
            }
        }

        private void OnSuddenDeathStarted()
        {
            // 表示（Alpha解除）
            SetAlpha(m_SuddenDeathTextTop, 1f);
            SetAlpha(m_SuddenDeathTextBottom, 1f);
            SetAlpha(m_SuddenDeathPanelTop, 1f);
            SetAlpha(m_SuddenDeathPanelBottom, 1f);

            // スクロール開始
            m_IsSuddenDeathScrolling = true;

            // 初期位置（右端からスタート）
            m_TopRect.anchoredPosition = new Vector2(m_ScreenWidth, m_TopRect.anchoredPosition.y);
            m_BottomRect.anchoredPosition = new Vector2(-m_ScreenWidth, m_BottomRect.anchoredPosition.y);
        }

        private int? TryGetScore(string role)
        {
            if (m_State == null || m_State.Players == null) return null;

            foreach (var player in m_State.Players.Values)
            {
                if (player == null) continue;
                if (player.Role == role)
                {
                    return player.Score;
                }
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

        private void Update()
        {
            if (!m_IsSuddenDeathScrolling) return;

            // 上のテキスト：右 → 左
            m_TopRect.anchoredPosition += Vector2.left * m_ScrollSpeed * Time.deltaTime;

            if (m_TopRect.anchoredPosition.x < -m_ScreenWidth)
            {
                m_TopRect.anchoredPosition = new Vector2(m_ScreenWidth, m_TopRect.anchoredPosition.y);
            }

            // 下のテキスト：左 → 右
            m_BottomRect.anchoredPosition += Vector2.right * m_ScrollSpeed * Time.deltaTime;

            if (m_BottomRect.anchoredPosition.x > m_ScreenWidth)
            {
                m_BottomRect.anchoredPosition = new Vector2(-m_ScreenWidth, m_BottomRect.anchoredPosition.y);
            }
        }

        private void SetAlpha(TMP_Text text, float alpha)
        {
            if (text == null) return;
            var c = text.color;
            c.a = alpha;
            text.color = c;
        }

        private void SetAlpha(GameObject obj, float alpha)
        {
            if (obj == null) return;
            var img = obj.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }
    }
}
