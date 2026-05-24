using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace GamblingAction.UI
{
	// ボタンの OnClick 等から呼んで指定シーンへ遷移する。
	// 遷移先はインスペクタで設定（既定はゲーム本体の Boot）。
	public class SceneLoader : MonoBehaviour
	{
		[FormerlySerializedAs("sceneName")]
		[SerializeField, Tooltip("遷移先シーン名。Build Settings に登録されている必要がある")]
		private string m_SceneName = "Boot";

		[SerializeField, Tooltip("Start 時に自動で遷移する。ボタンの無い Bootstrap シーン用")]
		private bool m_LoadOnStart = false;

		private void Start()
		{
			if (m_LoadOnStart) Load();
		}

		// インスペクタで設定済みのシーンへ遷移する（ボタンの OnClick から呼ぶ想定）。
		public void Load()
		{
			LoadScene(m_SceneName);
		}

		// シーン名を指定して遷移する。
		public void LoadScene(string sceneName)
		{
			if (string.IsNullOrEmpty(sceneName))
			{
				Debug.LogError("[SceneLoader] sceneName が未設定です");
				return;
			}
			SceneManager.LoadScene(sceneName);
		}
	}
}
