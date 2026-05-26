using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GamblingAction.Gameplay.Editor
{
	// PIE（Play In Editor）開始時に node server.js を別 cmd ウィンドウで自動起動し、
	// PIE 終了時に自動で停止する。手動で start_server.bat を起動する手間を省くためのもの。
	// ログは従来通り別ウィンドウにそのまま出るので debug しやすい。
	[InitializeOnLoad]
	public static class PlayModeServerLauncher
	{
		// server.js とその node_modules があるフォルダ。
		private const string k_ServerRelativePath = "Assets/StreamingAssets/Server";
		// サーバが使うポート。停止時の取りこぼし対策で、このポートの残留プロセスも掃除する。
		private const string k_Port = "3000";
		// 起動した node プロセスの PID を domain reload をまたいで保持するためのキー。
		// static フィールドは PIE 開始時の domain reload で消えるため SessionState を使う。
		private const string k_PidKey = "PlayModeServerLauncher.Pid";

		static PlayModeServerLauncher()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			switch (state)
			{
				// 再生ボタンを押し、PIE に入る直前。ここでサーバを起動する。
				case PlayModeStateChange.ExitingEditMode:
					StartServer();
					break;
				// PIE を終了し、編集モードに戻る直前。ここでサーバを停止する。
				case PlayModeStateChange.ExitingPlayMode:
					StopServer();
					break;
			}
		}

		private static void StartServer()
		{
			// MPPM の仮想クローン（副本）ではサーバを起動しない。主エディタのみが起動する。
			if (!IsMainEditor())
				return;

			string serverDir = Path.GetFullPath(k_ServerRelativePath);

			if (!File.Exists(Path.Combine(serverDir, "server.js")))
			{
				Debug.LogWarning($"[Server] server.js が見つかりません: {serverDir}。自動起動をスキップします。");
				return;
			}

			if (!Directory.Exists(Path.Combine(serverDir, "node_modules")))
			{
				Debug.LogWarning("[Server] node_modules がありません。先に start_server.bat を一度実行して npm install してください。今回は自動起動をスキップします。");
				return;
			}

			// PIE 中の二重起動と、前回の異常終了で残ったプロセスを防ぐため、起動前にポートを掃除する。
			KillByPort();

			// 別 cmd ウィンドウで node server.js を実行する。ウィンドウを残すことで
			// 従来の start_server.bat と同じくログをそのまま確認できる。
			var psi = new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = $"/c node server.js",
				WorkingDirectory = serverDir,
				UseShellExecute = true,
				CreateNoWindow = false,
				WindowStyle = ProcessWindowStyle.Normal,
			};

			try
			{
				var process = Process.Start(psi);
				if (process != null)
				{
					SessionState.SetInt(k_PidKey, process.Id);
					Debug.Log("[Server] node server.js を自動起動しました（別ウィンドウ）。");
				}
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[Server] 自動起動に失敗しました: {e.Message}");
			}
		}

		private static void StopServer()
		{
			// 副本は起動していないため停止もしない。主実例のサーバを誤って落とさないため。
			if (!IsMainEditor())
				return;

			int pid = SessionState.GetInt(k_PidKey, 0);
			if (pid != 0)
			{
				// 起動した cmd プロセスをツリーごと停止する（/c node を子に持つため /t が必要）。
				RunHidden("taskkill", $"/f /t /pid {pid}");
				SessionState.EraseInt(k_PidKey);
			}

			// cmd ウィンドウを手動で閉じた等で PID では取りこぼす場合に備え、ポートでも掃除する。
			KillByPort();
			Debug.Log("[Server] サーバを停止しました。");
		}

		// ポート 3000 を LISTENING しているプロセスを探して停止する。
		private static void KillByPort()
		{
			RunHidden("cmd.exe",
				$"/c for /f \"tokens=5\" %a in ('netstat -aon ^| findstr :{k_Port} ^| findstr LISTENING') do taskkill /f /pid %a");
		}

		// ウィンドウを出さずにコマンドを同期実行する補助。
		private static void RunHidden(string fileName, string arguments)
		{
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = fileName,
					Arguments = arguments,
					UseShellExecute = false,
					CreateNoWindow = true,
				};
				using (var p = Process.Start(psi))
				{
					p?.WaitForExit(3000);
				}
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[Server] プロセス停止コマンドの実行に失敗しました: {e.Message}");
			}
		}

		// この Unity 実例が MPPM の主エディタかどうかを返す。仮想クローン（副本）では false。
		// Editor asmdef が MPPM パッケージに直接依存しないよう、リフレクションで判定する。
		// MPPM が見つからない場合（パッケージ未導入・単独起動）は主エディタとして扱う。
		private static bool IsMainEditor()
		{
			try
			{
				var type = System.Type.GetType(
					"Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode");
				if (type == null)
					return true;

				var property = type.GetProperty("IsMainEditor",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				if (property != null && property.GetValue(null) is bool isMain)
					return isMain;

				return true;
			}
			catch
			{
				return true;
			}
		}
	}
}
