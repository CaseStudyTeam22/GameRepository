using GamblingAction.Domain;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	public class ScoreManager : MonoBehaviour
	{		
		[Header("P1ScoreImages")]
        [FormerlySerializedAs("p1ScoreImages")]
        [SerializeField, Tooltip("P1 のスコアを表示する 画像")]
        private List<Image> m_P1ScoreImages;

        [Header("P2ScoreImages")]
        [FormerlySerializedAs("p2ScoreImages")]
        [SerializeField, Tooltip("P2 のスコアを表示する 画像")]
        private List<Image> m_P2ScoreImages;

        [Header("Player Roles")]
		[SerializeField, Tooltip("P1 を判定する role 名")]
		private string m_P1Role = "P1";

		[SerializeField, Tooltip("P2 を判定する role 名")]
		private string m_P2Role = "P2";

		[SerializeField, Tooltip("対象プレイヤーが見つからない場合に表示する文字列")]
		private string m_MissingPlayerText = "-";

        [SerializeField] 
		private Vector2 m_LeftPosition;
        
		[SerializeField] 
		private Vector2 m_RightPosition;

        private IGameState m_State;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[ScoreManager] GameStateLocator.Current is null");
				return;
			}

			m_State.OnStateInitialized += RefreshScores;
			m_State.OnPlayersChanged += RefreshScores;

			RefreshScores();
		}

		private void OnDestroy()
		{
			if (m_State == null) return;

			m_State.OnStateInitialized -= RefreshScores;
			m_State.OnPlayersChanged -= RefreshScores;
		}

		private void RefreshScores()
		{
            SetScoreImage(m_P1ScoreImages, TryGetScore(m_P1Role));
            SetScoreImage(m_P2ScoreImages, TryGetScore(m_P2Role));

            UpdatePosition();
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

		private void SetScoreImage(List<Image> images, int? score)
		{
			if (images == null) return;

			for (int i = 0; i < images.Count; ++i)
			{
				if (i + 1 <= score.Value)
				{
                    images[i].color = score.HasValue ? Color.yellow : Color.gray;
                }
				else
				{
                    images[i].color = Color.gray;
                }
            }
        }

		// スコア表示の位置を、現在のプレイヤーIDに応じて更新する
		private void UpdatePosition()
		{
			if (m_State?.Me == null)
				return;

            var p1Rect = m_P1ScoreImages[0] != null
                ? m_P1ScoreImages[0].transform.parent.GetComponent<RectTransform>()
                : null;

            var p2Rect = m_P2ScoreImages[0] != null
                ? m_P2ScoreImages[0].transform.parent.GetComponent<RectTransform>()
                : null;

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