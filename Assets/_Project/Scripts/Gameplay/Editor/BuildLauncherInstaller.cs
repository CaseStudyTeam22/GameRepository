using System.IO;
using UnityEditor;
using UnityEngine;

namespace GamblingAction.Gameplay.Editor
{
	// ビルド出力フォルダ（game.exe と同じ階層）に setup-firewall.bat を配置するためのメニュー。
	// 配布先のユーザーが奥のフォルダを掘らずに、最外層から実行できるようにするのが目的。
	// 元バッチは StreamingAssets/Server/setup-firewall.bat にあるが、最外層に置く版は
	// node.exe への相対パスが異なるため、内容をその場で生成して書き出す。
	public static class BuildLauncherInstaller
	{
		private const string k_MenuPath = "Tools/GamblingAction/ビルド出力に setup-firewall.bat を配置";
		// EditorPrefs で前回選んだ出力フォルダを覚えておく。毎回選び直す手間を減らすため。
		private const string k_LastBuildDirKey = "GamblingAction.BuildLauncherInstaller.LastDir";

		[MenuItem(k_MenuPath)]
		private static void Install()
		{
			string startDir = EditorPrefs.GetString(k_LastBuildDirKey, "");
			string buildDir = EditorUtility.OpenFolderPanel(
				"ビルド出力フォルダ（game.exe があるフォルダ）を選択",
				startDir, "");
			if (string.IsNullOrEmpty(buildDir)) return;

			string[] exes = Directory.GetFiles(buildDir, "*.exe");
			string productExe = null;
			foreach (string e in exes)
			{
				string name = Path.GetFileNameWithoutExtension(e);
				string dataDir = Path.Combine(buildDir, name + "_Data");
				if (Directory.Exists(dataDir)) { productExe = e; break; }
			}

			if (productExe == null)
			{
				EditorUtility.DisplayDialog(
					"setup-firewall.bat の配置",
					"このフォルダにはビルド済みの game.exe（と <name>_Data フォルダ）が見つかりません。",
					"OK");
				return;
			}

			string productName = Path.GetFileNameWithoutExtension(productExe);
			string nodeRelative = Path.Combine(productName + "_Data", "StreamingAssets", "Server", "node.exe");
			string nodeAbsolute = Path.Combine(buildDir, nodeRelative);
			if (!File.Exists(nodeAbsolute))
			{
				EditorUtility.DisplayDialog(
					"setup-firewall.bat の配置",
					$"node.exe が見つかりません:\n{nodeAbsolute}\nビルドが正しく完了しているか確認してください。",
					"OK");
				return;
			}

			string outBatPath = Path.Combine(buildDir, "setup-firewall.bat");
			try
			{
				File.WriteAllText(outBatPath, BuildBatContent(nodeRelative), new System.Text.UTF8Encoding(true));
				EditorPrefs.SetString(k_LastBuildDirKey, buildDir);
				EditorUtility.RevealInFinder(outBatPath);
				Debug.Log($"[BuildLauncherInstaller] 配置完了: {outBatPath}");
			}
			catch (System.IO.IOException e)
			{
				EditorUtility.DisplayDialog(
					"setup-firewall.bat の配置",
					$"書き込みに失敗しました:\n{e.Message}",
					"OK");
			}
		}

		// 最外層用バッチ。node.exe への相対パスを引数で受け取って生成する。
		private static string BuildBatContent(string nodeRelative)
		{
			string escapedRelative = nodeRelative.Replace("/", "\\");
			return
				"@echo off\r\n" +
				"chcp 65001 > nul\r\n" +
				"setlocal\r\n" +
				"\r\n" +
				"rem このバッチは「管理者として実行」してください。\r\n" +
				"rem ゲーム同梱の node.exe に Windows ファイアウォールの受信許可を与えます。\r\n" +
				"\r\n" +
				$"set \"NODE_PATH=%~dp0{escapedRelative}\"\r\n" +
				"\r\n" +
				"if not exist \"%NODE_PATH%\" (\r\n" +
				"    echo [エラー] node.exe が見つかりません:\r\n" +
				"    echo   %NODE_PATH%\r\n" +
				"    pause\r\n" +
				"    exit /b 1\r\n" +
				")\r\n" +
				"\r\n" +
				"net session >nul 2>&1\r\n" +
				"if errorlevel 1 (\r\n" +
				"    echo [エラー] 管理者権限が必要です。\r\n" +
				"    echo このバッチを右クリックして「管理者として実行」してください。\r\n" +
				"    pause\r\n" +
				"    exit /b 1\r\n" +
				")\r\n" +
				"\r\n" +
				"echo Windows ファイアウォールに受信許可を追加します:\r\n" +
				"echo   %NODE_PATH%\r\n" +
				"echo.\r\n" +
				"\r\n" +
				"netsh advfirewall firewall delete rule name=\"GamblingAction Server\" > nul 2>&1\r\n" +
				"\r\n" +
				"netsh advfirewall firewall add rule ^\r\n" +
				"    name=\"GamblingAction Server\" ^\r\n" +
				"    dir=in ^\r\n" +
				"    action=allow ^\r\n" +
				"    program=\"%NODE_PATH%\" ^\r\n" +
				"    enable=yes ^\r\n" +
				"    profile=any\r\n" +
				"\r\n" +
				"if errorlevel 1 (\r\n" +
				"    echo.\r\n" +
				"    echo [エラー] 受信許可の追加に失敗しました。\r\n" +
				"    pause\r\n" +
				"    exit /b 1\r\n" +
				")\r\n" +
				"\r\n" +
				"echo.\r\n" +
				"echo 設定が完了しました。ゲームを起動してください。\r\n" +
				"pause\r\n" +
				"endlocal\r\n";
		}
	}
}
