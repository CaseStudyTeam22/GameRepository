using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GamblingAction.Gameplay.Editor
{
	// StreamingAssets/Server/ の Node 依存（node_modules）を初回だけ取得するためのメニュー。
	// node_modules はリポジトリに含めていないため、clone 直後はこのメニューを一度実行する必要がある。
	// 実行には開発 PC 側に Node.js がインストールされている前提。
	public static class ServerDependenciesInstaller
	{
		private const string k_MenuPath = "Tools/GamblingAction/サーバー依存を導入 (npm install)";
		private const string k_ServerRelativePath = "Assets/StreamingAssets/Server";

		[MenuItem(k_MenuPath)]
		private static void Install()
		{
			string serverDir = Path.GetFullPath(k_ServerRelativePath);

			if (!File.Exists(Path.Combine(serverDir, "package.json")))
			{
				EditorUtility.DisplayDialog(
					"サーバー依存の導入",
					$"package.json が見つかりません:\n{serverDir}",
					"OK");
				return;
			}

			if (Directory.Exists(Path.Combine(serverDir, "node_modules")))
			{
				bool reinstall = EditorUtility.DisplayDialog(
					"サーバー依存の導入",
					"node_modules は既に存在します。再導入しますか？",
					"再導入する", "キャンセル");
				if (!reinstall) return;
			}

			// npm.cmd を別ウィンドウで開いて npm install を流す。
			// 別ウィンドウにする理由は、進行状況とエラーをそのまま画面で確認できるようにするため。
			var psi = new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = "/k npm install",
				WorkingDirectory = serverDir,
				UseShellExecute = true,
				CreateNoWindow = false,
				WindowStyle = ProcessWindowStyle.Normal,
			};

			try
			{
				Process.Start(psi);
				Debug.Log($"[ServerDeps] npm install を別ウィンドウで開始しました: {serverDir}");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[ServerDeps] 起動に失敗: {e.Message}\nNode.js がインストールされているか確認してください。");
			}
		}
	}
}
