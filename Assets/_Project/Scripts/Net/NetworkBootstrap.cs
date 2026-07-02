using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GamblingAction.Net
{
	// 接続先 URL を自動解決する。
	// 1) UDP で同一 LAN のホスト通知を探す。発見すればクライアントとして接続。
	// 2) 見つからなければ自分がホストになる（node.exe を起動）。
	// 3) ポート 3000 が他プロセスに使用されていて node を起動できなかった場合は、
	//    そのプロセスがホストを取ったということなので、もう一度短く UDP を聞いて
	//    そのホストに接続する。
	public sealed class NetworkBootstrap : IDisposable
	{
		// 探索フェーズの待ち時間。
		private static readonly TimeSpan k_DiscoveryTimeout = TimeSpan.FromSeconds(3);
		// ポート競合後の再探索の待ち時間。相手はもうポートを掴んでいるはずなので短くて良い。
		private static readonly TimeSpan k_RetryDiscoveryTimeout = TimeSpan.FromSeconds(2);
		// 自分がホストになった直後、もう一台も同時にホストになっていないか確認する待ち時間。
		// 二人がほぼ同時に起動した場合だけ衝突するため、双方のサーバが立ち上がって
		// 通知し合うのに十分な短い時間で足りる。
		private static readonly TimeSpan k_HostConflictTimeout = TimeSpan.FromSeconds(3);

		public string ResolvedUrl { get; private set; }
		public bool IsHost { get; private set; }

		private HostServerProcess m_Host;

		// 自動探索→必要ならホスト起動、まで一気に行う。最終 URL を返す。
		// 失敗時は null。
		public async Task<string> ResolveAsync(CancellationToken ct)
		{
			// 1) まず周りに既存ホストが居ないか探す。
			Debug.Log("[NetworkBootstrap] LAN 上のホストを探索中…");
			var found = await LanDiscovery.ListenAsync(k_DiscoveryTimeout, excludeOwnPid: 0, ct);
			if (found.IsValid)
			{
				ResolvedUrl = $"http://{found.HostIp}:{found.Port}";
				IsHost = false;
				Debug.Log($"[NetworkBootstrap] ホスト発見 → クライアントとして接続: {ResolvedUrl}");
				return ResolvedUrl;
			}

			// 2) 居なければ自分がホストになるべく node を起動。ポート競合は StartAsync が検出する。
			Debug.Log("[NetworkBootstrap] ホストとして node.exe を起動");
			m_Host = new HostServerProcess();
			var startResult = await m_Host.StartAsync(ct);

			if (startResult == HostServerProcess.StartResult.Success)
			{
				// 起動した直後に、もう一台も同時にホストになっていないかを確認する。
				// 探索は短い固定時間しか聞かないため、二人が時間差で起動すると
				// 互いに相手を見つけられず「双方がホスト」になり、別々のサーバに
				// 繋がって相手が見えなくなる。そこで起動後にもう一度聞き、別のホストが
				// 居たら PID の小さい方をホストとして残し、大きい方はクライアントへ降格する。
				var rival = await LanDiscovery.ListenAsync(k_HostConflictTimeout, excludeOwnPid: m_Host.Pid, ct);
				if (rival.IsValid && m_Host.Pid > rival.Pid)
				{
					// 相手の方がホストを続ける。自分の node を止めてクライアントとして繋ぐ。
					Debug.Log($"[NetworkBootstrap] ホスト衝突を検出（自PID={m_Host.Pid} > 相手PID={rival.Pid}）→ クライアントへ降格");
					m_Host.Dispose();
					m_Host = null;
					ResolvedUrl = $"http://{rival.HostIp}:{rival.Port}";
					IsHost = false;
					return ResolvedUrl;
				}

				// Windows では "localhost" が ::1（IPv6）に解決されることがあり、
				// 環境によっては socket.io 接続が成立せず無応答になる。明示的に IPv4 を指定する。
				ResolvedUrl = $"http://127.0.0.1:{HostServerProcess.ServerPort}";
				IsHost = true;
				Debug.Log($"[NetworkBootstrap] ホスト確定: {ResolvedUrl}");
				return ResolvedUrl;
			}

			// ポート競合などで node を起動できなかった。子プロセスを片付ける。
			m_Host.Dispose();
			m_Host = null;

			if (startResult == HostServerProcess.StartResult.PortAlreadyInUse)
			{
				// 3) 既に他プロセスがホストを取った。短く再探索して接続先を取得する。
				Debug.Log("[NetworkBootstrap] 他プロセスがホスト中 → 再探索してクライアント接続");
				found = await LanDiscovery.ListenAsync(k_RetryDiscoveryTimeout, excludeOwnPid: 0, ct);
				if (found.IsValid)
				{
					ResolvedUrl = $"http://{found.HostIp}:{found.Port}";
					IsHost = false;
					Debug.Log($"[NetworkBootstrap] 再探索でホスト発見: {ResolvedUrl}");
					return ResolvedUrl;
				}
				// 通知が間に合っていない／同一マシン内ローカルテストなどでも繋がるよう 127.0.0.1 にフォールバック。
				// "localhost" だと Windows で IPv6 解決され接続が無応答になることがあるため、IPv4 を明示する。
				ResolvedUrl = $"http://127.0.0.1:{HostServerProcess.ServerPort}";
				IsHost = false;
				Debug.LogWarning($"[NetworkBootstrap] 通知が届かないため 127.0.0.1 フォールバック: {ResolvedUrl}");
				return ResolvedUrl;
			}

			// その他の失敗（node.exe 不在など）は致命的。呼び出し側でエラー処理する。
			return null;
		}

		public void Dispose()
		{
			m_Host?.Dispose();
			m_Host = null;
		}
	}
}
