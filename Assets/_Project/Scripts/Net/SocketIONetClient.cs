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
		readonly SocketIOClient _socket;
		readonly Dictionary<string, Action<string>> _handlers = new();

		public bool IsConnected => _socket.IsConnected;

		public event Action OnConnected;
		public event Action OnDisconnected;

		public SocketIONetClient()
		{
			_socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());
			_socket.OnConnected    += () => UnityMainThreadDispatcher.Enqueue(() => OnConnected?.Invoke());
			_socket.OnDisconnected += () => UnityMainThreadDispatcher.Enqueue(() => OnDisconnected?.Invoke());
		}

		public void Connect(string url)
		{
			Debug.Log($"[Net] Connecting to {url}");
			_socket.Connect(url);
		}

		public void Disconnect()
		{
			_socket.Disconnect();
		}

		public void Emit(string eventName, object payload)
		{
			_socket.Emit(eventName, payload);
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
			_handlers[eventName] = raw;
			_socket.On(eventName, raw);
		}

		static bool LooksLikeJson(string s)
		{
			if (string.IsNullOrEmpty(s)) return false;
			char c = s[0];
			return c == '{' || c == '[' || c == '"' || c == 't' && s == "true" || c == 'f' && s == "false" || c == 'n' && s == "null"
				   || c == '-' || (c >= '0' && c <= '9');
		}

		public void On(string eventName, Action handler)
		{
			Action<string> raw = _ => handler();
			_handlers[eventName] = raw;
			_socket.On(eventName, raw);
		}

		public void Off(string eventName)
		{
			if (_handlers.TryGetValue(eventName, out var raw))
			{
				_socket.Off(eventName, raw);
				_handlers.Remove(eventName);
			}
		}

		public void Dispose()
		{
			_socket.Shutdown();
		}
	}
}
