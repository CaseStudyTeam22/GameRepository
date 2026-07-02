using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;
using SocketIOUnity.UnityIntegration;
using UnityEngine;

namespace GamblingAction.Net
{
	public class SocketIONetClient : INetClient, IDisposable
	{
		private readonly SocketIOClient m_Socket;
		private readonly Dictionary<string, Action<string>> m_Handlers = new();

		public bool IsConnected => m_Socket.IsConnected;

		public event Action OnConnected;
		public event Action OnDisconnected;

		public SocketIONetClient()
		{
			m_Socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());
			m_Socket.OnConnected    += () => UnityMainThreadDispatcher.Enqueue(() => OnConnected?.Invoke());
			m_Socket.OnDisconnected += () => UnityMainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke());
		}

		public void Connect(string url)
		{
			Debug.Log($"[Net] Connecting to {url}");
			m_Socket.Connect(url);
		}

		public void Disconnect()
		{
			m_Socket.Disconnect();
		}

		public void Emit(string eventName, object payload)
		{
			m_Socket.Emit(eventName, payload);
		}

		public void On<T>(string eventName, Action<T> handler)
		{
			Action<string> raw = json =>
			{
				T parsed;
				try
				{
					if (string.IsNullOrEmpty(json))
					{
						parsed = default;
					}
					else if (typeof(T) == typeof(string) && !LooksLikeJson(json))
					{
						// Server may emit a bare string payload (e.g. player_left = socket id).
						parsed = (T)(object)json;
					}
					else
					{
						parsed = JsonConvert.DeserializeObject<T>(json);
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"[Net] Failed to parse '{eventName}': {ex.Message}\nPayload: {json}");
					return;
				}
                handler(parsed);
			};
			m_Handlers[eventName] = raw;
			m_Socket.On(eventName, raw);
		}

		private static bool LooksLikeJson(string s)
		{
			if (string.IsNullOrEmpty(s)) return false;
			char c = s[0];
			return c == '{' || c == '[' || c == '"' || c == 't' && s == "true" || c == 'f' && s == "false" || c == 'n' && s == "null"
				   || c == '-' || (c >= '0' && c <= '9');
		}

		public void On(string eventName, Action handler)
		{
			Action<string> raw = _ => handler();
			m_Handlers[eventName] = raw;
			m_Socket.On(eventName, raw);
		}

		public void Off(string eventName)
		{
			if (m_Handlers.TryGetValue(eventName, out var raw))
			{
				m_Socket.Off(eventName, raw);
				m_Handlers.Remove(eventName);
			}
		}

		public void Dispose()
		{
			m_Socket.Shutdown();
		}
	}
}
