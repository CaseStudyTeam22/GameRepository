using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GamblingAction.Gameplay.Editor
{
	// 旧 PIE フロー：再生開始時に node server.js を別 cmd ウィンドウで起動。
	// 現在は NetworkBootstrap（StreamingAssets/Server/node.exe を子プロセスで起動）に
	// 役割を移譲済み。既定では無効。Tools > GamblingAction > Use Legacy ... で切替可。
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
		// この EditorPref が true のときだけ旧フローを動かす。既定は無効。
		private const string k_LegacyEnabledKey = "GamblingAction.PlayModeServerLauncher.Legacy";
		private const string k_MenuPath = "Tools/GamblingAction/再生時に旧サーバー自動起動を使う";

		static PlayModeServerLauncher()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		[MenuItem(k_MenuPath)]
		private static void ToggleLegacy()
		{
			bool next = !EditorPrefs.GetBool(k_LegacyEnabledKey, false);
			EditorPrefs.SetBool(k_LegacyEnabledKey, next);
			Debug.Log($"[Server] 旧サーバー自動起動 = {(next ? "有効" : "無効")}");
		}

		[MenuItem(k_MenuPath, true)]
		private static bool ToggleLegacyValidate()
		{
			Menu.SetChecked(k_MenuPath, EditorPrefs.GetBool(k_LegacyEnabledKey, false));
			return true;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			// 既定では NetworkBootstrap が StreamingAssets/Server/node.exe を起動するので何もしない。
			if (!EditorPrefs.GetBool(k_LegacyEnabledKey, false)) return;

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
