using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.UI
{
	public class ScoreManager : MonoBehaviour
	{
		[Header("Score Texts")]
		[FormerlySerializedAs("p1ScoreText")]
		[SerializeField, Tooltip("P1 のスコアを表示する TextMeshPro テキスト")]
		private TMP_Text m_P1ScoreText;

		[FormerlySerializedAs("p2ScoreText")]
		[SerializeField, Tooltip("P2 のスコアを表示する TextMeshPro テキスト")]
		private TMP_Text m_P2ScoreText;

		[Header("Player Roles")]
		[SerializeField, Tooltip("P1 を判定する role 名")]
		private string m_P1Role = "P1";

		[SerializeField, Tooltip("P2 を判定する role 名")]
		private string m_P2Role = "P2";

		[SerializeField, Tooltip("対象プレイヤーが見つからない場合に表示する文字列")]
		private string m_MissingPlayerText = "-";

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
			SetScoreText(m_P1ScoreText, TryGetScore(m_P1Role));
			SetScoreText(m_P2ScoreText, TryGetScore(m_P2Role));
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
	}
}
