using GamblingAction.Domain;
using GamblingAction.Net;
using UnityEngine;

namespace GamblingAction.Bootstrap
{
	public class IntegrationTestProbe : MonoBehaviour
	{
		[SerializeField] string serverUrl = "http://localhost:3000";

		SocketIONetClient _net;
		GameState _state;

		void Start()
		{
			Debug.Log("[Probe] Start() called");
			_net = new SocketIONetClient();
			_state = new GameState(_net);

			_state.OnConnectionChanged += connected =>
				Debug.Log($"[Probe] connection: {connected}");
			_state.OnStateInitialized += () =>
				Debug.Log($"[Probe] init done. MyId={_state.MyId} Grid={_state.GridSize} Players={_state.Players.Count}");
			_state.OnPlayersChanged += () =>
			{
				var me = _state.Me;
				Debug.Log($"[Probe] players changed. me={(me == null ? "null" : $"{me.Role}@({me.X},{me.Y}) chips={me.Chips} stamina={me.Stamina}")}");
			};
			_state.OnItemsChanged += () =>
				Debug.Log($"[Probe] items changed. count={_state.Items.Count}");
			_state.OnBeatChanged += () =>
				Debug.Log($"[Probe] beat={_state.CurrentBeat} timeLeft={_state.TimeLeft} active={_state.GameActive}");
			_state.OnPhaseChanged += phase =>
				Debug.Log($"[Probe] phase → {phase}");
			_state.OnGameEvents += events =>
				Debug.Log($"[Probe] game_events x{events.Length}: {string.Join(",", System.Array.ConvertAll(events, e => e.Type))}");
			_state.OnRoundOver += winner =>
				Debug.Log($"[Probe] round_over winner={winner}");
			_state.OnGameOver += winner =>
				Debug.Log($"[Probe] game_over winner={winner}");
			_state.OnPlayerLeft += id =>
				Debug.Log($"[Probe] player_left {id}");
			_state.OnWaitingForOthers += who =>
				Debug.Log($"[Probe] waiting_for {who}");

			_net.Connect(serverUrl);
		}

		void OnDestroy()
		{
			_state?.Dispose();
			_net?.Dispose();
		}
	}
}
