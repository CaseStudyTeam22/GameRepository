using GamblingAction.Domain;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections;

namespace GamblingAction.UI
{
    public class ScoreManager : MonoBehaviour
    {
        [Header("P1ScoreImages")]
        [FormerlySerializedAs("p1ScoreImages")]
        [SerializeField, Tooltip("P1 のスコア表示画像")]
        private List<Image> m_P1ScoreImages;

        [Header("P2ScoreImages")]
        [FormerlySerializedAs("p2ScoreImages")]
        [SerializeField, Tooltip("P2 のスコア表示画像")]
        private List<Image> m_P2ScoreImages;

        [Header("Player Roles")]
        [SerializeField, Tooltip("P1 を判定する role 名")]
        private string m_P1Role = "P1";

        [SerializeField, Tooltip("P2 を判定する role 名")]
        private string m_P2Role = "P2";

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
            m_State.OnSuddenDeathStarted -= OnSuddenDeathStarted;

            // 念のためパネルのグローを停止しておく
            var topGlow = m_SuddenDeathPanelTop != null ? m_SuddenDeathPanelTop.GetComponent<SuddenDeathPanelGlow>() : null;
            if (topGlow != null) topGlow.StopGlow();
            var bottomGlow = m_SuddenDeathPanelBottom != null ? m_SuddenDeathPanelBottom.GetComponent<SuddenDeathPanelGlow>() : null;
            if (bottomGlow != null) bottomGlow.StopGlow();
        }

        private void RefreshScores()
        {
            SetScoreImage(m_P1ScoreImages, TryGetScore(m_P1Role));
            SetScoreImage(m_P2ScoreImages, TryGetScore(m_P2Role));

            UpdatePosition();
            CheckSuddenDeath();
        }

        private void SetScoreImage(List<Image> images, int? score)
        {
            if (images == null) return;

            for (int i = 0; i < images.Count; ++i)
            {
                if (score.HasValue && i + 1 <= score.Value)
                {
                    images[i].color = Color.yellow;
                }
                else
                {
                    images[i].color = Color.gray;
                }
            }
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



                m_State.NotifySuddenDeathRequested();
            }
        }

        private void OnSuddenDeathStarted()
        {
            // 表示（Alpha解除）
            SetAlpha(m_SuddenDeathTextTop, 1f);
            SetAlpha(m_SuddenDeathTextBottom, 1f);
            SetAlpha(m_SuddenDeathPanelTop, 1f);
            SetAlpha(m_SuddenDeathPanelBottom, 1f);

            StartCoroutine(FadeOutDeathText());

            // スクロール開始
            m_IsSuddenDeathScrolling = true;

            // 初期位置（右端からスタート）
            m_TopRect.anchoredPosition = new Vector2(m_ScreenWidth, m_TopRect.anchoredPosition.y);
            m_BottomRect.anchoredPosition = new Vector2(-m_ScreenWidth, m_BottomRect.anchoredPosition.y);

            // パネルのグロー開始（SuddenDeathPanelGlow に任せる）
            var topGlow = m_SuddenDeathPanelTop != null ? m_SuddenDeathPanelTop.GetComponent<SuddenDeathPanelGlow>() : null;
            if (topGlow != null) topGlow.StartGlow();
            var bottomGlow = m_SuddenDeathPanelBottom != null ? m_SuddenDeathPanelBottom.GetComponent<SuddenDeathPanelGlow>() : null;
            if (bottomGlow != null) bottomGlow.StartGlow();
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

        private void UpdatePosition()
        {
            if (m_State?.Me == null)
                return;

            var p1Rect = m_P1ScoreImages != null && m_P1ScoreImages.Count > 0 && m_P1ScoreImages[0] != null
                ? m_P1ScoreImages[0].transform.parent.GetComponent<RectTransform>()
                : null;

            var p2Rect = m_P2ScoreImages != null && m_P2ScoreImages.Count > 0 && m_P2ScoreImages[0] != null
                ? m_P2ScoreImages[0].transform.parent.GetComponent<RectTransform>()
                : null;

            if (p1Rect == null || p2Rect == null)
                return;

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

        private IEnumerator FadeOutDeathText()
        {
            // まず Alpha を 1 にする
            SetAlpha(m_DeathText, 1f);

            // 5秒待つ
            yield return new WaitForSeconds(5f);

            // Alpha を 0 にする
            SetAlpha(m_DeathText, 0f);
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