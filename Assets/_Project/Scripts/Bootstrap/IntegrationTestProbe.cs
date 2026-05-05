using GamblingAction.Domain;
using GamblingAction.Net;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Bootstrap
{
	public class IntegrationTestProbe : MonoBehaviour
	{
		[FormerlySerializedAs("serverUrl")]
		[SerializeField] private string m_ServerUrl = "http://localhost:3000";

		private SocketIONetClient m_Net;
		private GameState m_State;

		private void Start()
		{
			Debug.Log("[Probe] Start() called");
			m_Net = new SocketIONetClient();
			m_State = new GameState(m_Net);

			m_State.OnConnectionChanged += connected =>
				Debug.Log($"[Probe] connection: {connected}");
			m_State.OnStateInitialized += () =>
				Debug.Log($"[Probe] init done. MyId={m_State.MyId} Grid={m_State.GridSize} Players={m_State.Players.Count}");
			m_State.OnPlayersChanged += () =>
			{
				var me = m_State.Me;
				Debug.Log($"[Probe] players changed. me={(me == null ? "null" : $"{me.Role}@({me.X},{me.Y}) chips={me.Chips} stamina={me.Stamina}")}");
			};
			m_State.OnItemsChanged += () =>
				Debug.Log($"[Probe] items changed. count={m_State.Items.Count}");
			m_State.OnBeatChanged += () =>
				Debug.Log($"[Probe] beat={m_State.CurrentBeat} timeLeft={m_State.TimeLeft} active={m_State.GameActive}");
			m_State.OnPhaseChanged += phase =>
				Debug.Log($"[Probe] phase → {phase}");
			m_State.OnGameEvents += events =>
				Debug.Log($"[Probe] game_events x{events.Length}: {string.Join(",", System.Array.ConvertAll(events, e => e.Type))}");
			m_State.OnRoundOver += winner =>
				Debug.Log($"[Probe] round_over winner={winner}");
			m_State.OnGameOver += winner =>
				Debug.Log($"[Probe] game_over winner={winner}");
			m_State.OnPlayerLeft += id =>
				Debug.Log($"[Probe] player_left {id}");
			m_State.OnWaitingForOthers += who =>
				Debug.Log($"[Probe] waiting_for {who}");

			m_Net.Connect(m_ServerUrl);
		}

		private void OnDestroy()
		{
			m_State?.Dispose();
			m_Net?.Dispose();
		}
	}
}
