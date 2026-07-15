#if UNITY_EDITOR
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GamblingAction.Bootstrap
{
	// Bootstrap シーンを経由せずに単一シーンを直接 Play したときの補助。
	// 常駐の頭脳（GameInstaller）が無ければその場で生成し、AI 準備にして
	// 手動の準備操作なしでテストできるようにする。Editor 専用。
	public static class DebugBootstrapper
	{
		private const string k_BootstrapSceneName = "Bootstrap";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void EnsureGameInstaller()
		{
			// Bootstrap から正規に起動した場合は何もしない（正式フロー）。
			// 直前のデバッグ直起動で static が残っていても正式フローを汚さないよう復位する。
			string activeScene = SceneManager.GetActiveScene().name;
			if (activeScene == k_BootstrapSceneName || activeScene == "LobbyScene" || activeScene == "TitleScene")
			{
				GameInstaller.DebugAutoAIReady = false;
				return;
			}

			// 既に頭脳が居れば不要。
			if (GameStateLocator.Current != null) return;

			GameInstaller.DebugAutoAIReady = true;

			var go = new GameObject("GameInstaller (Debug)");
			go.AddComponent<GameInstaller>();
			Debug.Log("[DebugBootstrapper] 単一シーン直起動のため GameInstaller を生成（AI 準備）");
		}
	}
}
#endif
