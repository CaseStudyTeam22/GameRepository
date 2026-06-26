using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GamblingAction.Net
{
	// 同一 LAN 上で稼働中の GamblingAction サーバを UDP ブロードキャストで探す。
	// サーバ側（server.js の startLanBroadcast）が同じ MAGIC とポートで送信する。
	// 自プロセス送信分はループバックで自身も受信するため、PID で照合して除外する。
	public static class LanDiscovery
	{
		// server.js の LAN_DISCOVERY_MAGIC と一致させること。
		private const string k_Magic = "GAMBLINGACTION|7f3a4d9e";
		// server.js の LAN_DISCOVERY_PORT と一致させること。
		private const int k_Port = 38900;

		public readonly struct Result
		{
			public readonly string HostIp;
			public readonly int Port;
			// 通知を送ったサーバプロセスの PID。ホスト同士が衝突したとき、
			// どちらがホストを続けるかを PID で決めるために使う。
			public readonly int Pid;
			public Result(string ip, int port, int pid) { HostIp = ip; Port = port; Pid = pid; }
			public bool IsValid => !string.IsNullOrEmpty(HostIp);
		}

		// 指定時間だけ UDP を待ち受け、最初に届いた有効なホスト通知を返す。
		// 見つからなければ Result.IsValid == false で返る。
		// excludeOwnPid が非 0 のとき、その PID から届いたパケットは無視する。
		public static async Task<Result> ListenAsync(TimeSpan timeout, int excludeOwnPid, CancellationToken ct)
		{
			UdpClient client = null;
			try
			{
				// 同一マシン上で複数インスタンスが起動するケース（MPPM 等）に備えて
				// ReuseAddress を有効にしておく。
				client = new UdpClient(AddressFamily.InterNetwork);
				client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				client.Client.Bind(new IPEndPoint(IPAddress.Any, k_Port));
				client.EnableBroadcast = true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[LanDiscovery] UDP ポートの確保に失敗: {ex.Message}");
				client?.Dispose();
				return default;
			}

			var sw = Stopwatch.StartNew();
			try
			{
				while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
				{
					TimeSpan remaining = timeout - sw.Elapsed;
					if (remaining <= TimeSpan.Zero) break;

					// 受信タスクとタイムアウトタスクを先着で待つ。
					Task<UdpReceiveResult> recvTask = client.ReceiveAsync();
					Task delayTask = Task.Delay(remaining, ct);
					Task winner = await Task.WhenAny(recvTask, delayTask);

					if (winner != recvTask) return default;

					UdpReceiveResult r;
					try { r = await recvTask; }
					catch { continue; }

					if (r.Buffer == null || r.Buffer.Length == 0) continue;

					string text = Encoding.UTF8.GetString(r.Buffer);
					if (!TryParse(text, out string ip, out int port, out int pid)) continue;
					if (excludeOwnPid != 0 && pid == excludeOwnPid) continue;
					// 念のため：パケットに書かれた IP が空なら受信元 IP を使う。
					if (string.IsNullOrEmpty(ip)) ip = r.RemoteEndPoint.Address.ToString();
					return new Result(ip, port, pid);
				}
			}
			finally
			{
				client.Dispose();
			}

			return default;
		}

		// "GAMBLINGACTION|MAGIC|IP|PORT|PID" を解析する。
		private static bool TryParse(string text, out string ip, out int port, out int pid)
		{
			ip = null; port = 0; pid = 0;
			if (string.IsNullOrEmpty(text)) return false;
			if (!text.StartsWith(k_Magic, StringComparison.Ordinal)) return false;

			// MAGIC 内部にも '|' を含むため、末尾 3 要素（IP / PORT / PID）から取る。
			string[] parts = text.Split('|');
			if (parts.Length < 5) return false;
			ip = parts[parts.Length - 3];
			if (!int.TryParse(parts[parts.Length - 2], out port)) return false;
			if (!int.TryParse(parts[parts.Length - 1], out pid)) return false;
			return true;
		}
	}
}
