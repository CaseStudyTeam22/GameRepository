using System;

namespace GamblingAction.Net
{
	public interface INetClient
	{
		bool IsConnected { get; }

		event Action OnConnected;
		event Action OnDisconnected;

		void Connect(string url);
		void Disconnect();

		void Emit(string eventName, object payload);

		void On<T>(string eventName, Action<T> handler);
		void On(string eventName, Action handler);

		void Off(string eventName);
	}
}
