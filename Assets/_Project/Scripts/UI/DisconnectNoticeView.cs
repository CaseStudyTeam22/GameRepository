using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	// 相手が完全に切断した（サーバの猶予時間を過ぎて player_left が届いた）ときに、
	// 用意しておいた全画面パネルを表示して終了を促す。
	// 現状のサーバは片方が抜けた状態から復帰できないため、続行させずに再起動へ誘導する。
	//
	// GameInstaller prefab 配下の DisconnectNoticeCanvas に付け、この子ツリーだけで完結する。
	// パネルの生成はメニュー「GamblingAction/UI/相手切断パネルを生成」で行える。
	public class DisconnectNoticeView : MonoBehaviour
	{
		[SerializeField, Tooltip("相手切断時に表示するパネル（暗幕以下）。通常は非表示")]
		private GameObject m_Panel;
		[SerializeField, Tooltip("ゲームを終了するボタン")]
		private Button m_QuitButton;

		private IGameState m_State;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[DisconnectNotice] GameStateLocator.Current is null");
				return;
			}
			m_State.OnPlayerLeft += HandlePlayerLeft;

			if (m_QuitButton != null)
				m_QuitButton.onClick.AddListener(Quit);

			// prefab 側で表示のまま保存されていても、起動時は必ず隠す。
			if (m_Panel != null)
				m_Panel.SetActive(false);
		}

		private void OnDestroy()
		{
			if (m_State == null) return;
			m_State.OnPlayerLeft -= HandlePlayerLeft;
		}

		private void HandlePlayerLeft(string id)
		{
			// 自分が抜けた通知（自分の切断が確定した場合）は対象外。
			if (id == m_State.MyId) return;
			if (m_Panel != null)
				m_Panel.SetActive(true);
		}

		private static void Quit()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}
	}
}
