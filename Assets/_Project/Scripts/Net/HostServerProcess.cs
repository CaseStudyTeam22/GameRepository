using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GamblingAction.Net
{
	// StreamingAssets/Server に同梱された portable node.exe で server.js を起動・停止する。
	// ホスト確定時のみ呼ぶ。クライアント時は呼ばない（重複起動防止）。
	public sealed class HostServerProcess : IDisposable
	{
		// サーバが待ち受けるポート。server.js と一致させること。
		public const int ServerPort = 3000;

		// server.js が待ち受けを開始した時に最初に出力する行（前方一致で照合する）。
		// この行を観測してから「起動成功」と判定する。localhost への接続テストでは
		// 他プロセスのサーバに繋がってしまい誤判定するため使わない。
		private const string k_ReadyMarker = "[GamblingAction Server] Running on port";

		// node プロセスの上限起動待ち時間。
		private static readonly TimeSpan k_StartTimeout = TimeSpan.FromSeconds(10);
		// 起動確認のポーリング間隔。
		private static readonly TimeSpan k_PollInterval = TimeSpan.FromMilliseconds(100);

		public enum StartResult
		{
			Success,
			PortAlreadyInUse,
			OtherFailure,
		}

		private Process m_Process;
		private int m_Pid;
		private volatile bool m_ReadyObserved;
		private readonly StringBuilder m_StderrBuffer = new();
		private readonly object m_StderrLock = new();

		public int Pid => m_Pid;
		public bool IsRunning => m_Process != null && !m_Process.HasExited;

		// node.exe で server.js を起動し、待ち受け開始の合図が出るまで待つ。
		// 起動成功なら Success、ポートが他プロセスに使われていれば PortAlreadyInUse、
		// その他の失敗は OtherFailure を返す。呼び出し側はポート競合の場合に
		// クライアント側へ降格する想定。
		public async Task<StartResult> StartAsync(CancellationToken ct)
		{
			string serverDir = Path.Combine(Application.streamingAssetsPath, "Server");
			string nodeExe   = Path.Combine(serverDir, "node.exe");
			string serverJs  = Path.Combine(serverDir, "server.js");

			if (!File.Exists(nodeExe))
			{
				Debug.LogError(
					$"[HostServer] node.exe が見つかりません: {nodeExe}\n" +
					"Tools/download-node.ps1 を実行して portable Node を配置してください。");
				return StartResult.OtherFailure;
			}
			if (!File.Exists(serverJs))
			{
				Debug.LogError($"[HostServer] server.js が見つかりません: {serverJs}");
				return StartResult.OtherFailure;
			}

			var psi = new ProcessStartInfo
			{
				FileName = nodeExe,
				Arguments = "server.js",
				WorkingDirectory = serverDir,
				// 子プロセスの出力（通常ログとエラー出力）を Unity の Console に流すため
				// 親プロセス側で受け取れるようにする。
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			try
			{
				m_Process = new Process { StartInfo = psi, EnableRaisingEvents = true };
				m_Process.OutputDataReceived += OnStdout;
				m_Process.ErrorDataReceived  += OnStderr;
				if (!m_Process.Start())
				{
					Debug.LogError("[HostServer] node 起動に失敗しました");
					return StartResult.OtherFailure;
				}
				m_Pid = m_Process.Id;
				m_Process.BeginOutputReadLine();
				m_Process.BeginErrorReadLine();
				Debug.Log($"[HostServer] node.exe 起動 (PID={m_Pid})");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[HostServer] 起動中に例外: {ex.Message}");
				return StartResult.OtherFailure;
			}

			// 待ち受け開始の合図が出るか、プロセスが終了するか、タイムアウトかを待つ。
			var sw = Stopwatch.StartNew();
			while (sw.Elapsed < k_StartTimeout && !ct.IsCancellationRequested)
			{
				if (m_ReadyObserved) return StartResult.Success;
				if (m_Process.HasExited)
				{
					string errorOutput = DrainStderr();
					// Node がポート使用中エラーを出した場合のエラーコード。これに該当すれば
					// 他プロセスが先にホストを取ったと判断して、呼び出し側がクライアントに降格する。
					bool isPortConflict = errorOutput.IndexOf("EADDRINUSE", StringComparison.Ordinal) >= 0;
					if (isPortConflict)
					{
						// 他プロセスが先にポートを掴んでいる。エラー扱いせず情報ログにする。
						Debug.Log("[HostServer] ポート 3000 が他プロセスに使用されているため node 起動を中止");
					}
					else
					{
						Debug.LogError($"[HostServer] node が早期終了 (ExitCode={m_Process.ExitCode})\n{errorOutput}");
					}
					return isPortConflict ? StartResult.PortAlreadyInUse : StartResult.OtherFailure;
				}
				try { await Task.Delay(k_PollInterval, ct); }
				catch (OperationCanceledException) { return StartResult.OtherFailure; }
			}

			Debug.LogError("[HostServer] server.js の待ち受け開始合図を観測できませんでした");
			return StartResult.OtherFailure;
		}

		private void OnStdout(object _, DataReceivedEventArgs e)
		{
			if (string.IsNullOrEmpty(e.Data)) return;
			Debug.Log($"[Server] {e.Data}");
			if (e.Data.StartsWith(k_ReadyMarker, StringComparison.Ordinal))
				m_ReadyObserved = true;
		}

		// エラー出力は Console を埋め尽くさないようバッファに溜め、終了時にまとめて出力する。
		private void OnStderr(object _, DataReceivedEventArgs e)
		{
			if (string.IsNullOrEmpty(e.Data)) return;
			lock (m_StderrLock) m_StderrBuffer.AppendLine(e.Data);
		}

		private string DrainStderr()
		{
			lock (m_StderrLock)
			{
				string s = m_StderrBuffer.ToString();
				m_StderrBuffer.Clear();
				return s;
			}
		}

		public void Dispose()
		{
			if (m_Process == null) return;
			try
			{
				if (!m_Process.HasExited)
				{
					m_Process.Kill();
					m_Process.WaitForExit(2000);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[HostServer] 停止中に例外: {ex.Message}");
			}
			finally
			{
				m_Process.Dispose();
				m_Process = null;
			}
		}
	}
}
