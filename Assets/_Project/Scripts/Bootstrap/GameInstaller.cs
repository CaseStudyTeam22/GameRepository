using GamblingAction.Domain;
using GamblingAction.Net;
using UnityEngine;

namespace GamblingAction.Bootstrap
{
	public class GameInstaller : MonoBehaviour
	{
		public static GameInstaller Instance { get; private set; }

		[SerializeField] string serverUrl = "http://localhost:3000";
		[SerializeField] bool verboseProbeLogs = true;
		[SerializeField] bool autoReady = false;
		[SerializeField] bool autoReadyAsAI = false;
		[SerializeField] bool autoExchange = false;
		[SerializeField] int autoExchangeAmount = 10;
		[SerializeField] bool autoBuff = false;
		[SerializeField] string autoBuffId = "low_risk";

		SocketIONetClient _net;
		GameState _state;

		public IGameState State => _state;
		public INetClient Net => _net;

		void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;

			_net = new SocketIONetClient();
			_state = new GameState(_net);
			GameStateLocator.Set(_state);

			if (verboseProbeLogs)
				AttachProbe();

			if (autoReady)
				_state.OnStateInitialized += () => _state.SubmitReady(isAI: autoReadyAsAI);

			_state.OnPhaseChanged += phase =>
			{
				if (autoReadyAsAI) return;

				if (autoExchange && phase == GamePhase.Exchange)
					_state.SubmitExchange(autoExchangeAmount);

				if (autoBuff && phase == GamePhase.BuffSelection)
					_state.SubmitBuff(autoBuffId);
			};
		}

		void Start()
		{
			_net.Connect(serverUrl);
		}

		void AttachProbe()
		{
			_state.OnConnectionChanged += c => Debug.Log($"[Probe] connection={c}");
			_state.OnStateInitialized  += () => Debug.Log($"[Probe] init MyId={_state.MyId} Players={_state.Players.Count}");
			_state.OnPhaseChanged      += p => Debug.Log($"[Probe] phase={p}");
			_state.OnBeatChanged       += () => Debug.Log($"[Probe] beat={_state.CurrentBeat} t={_state.TimeLeft} active={_state.GameActive}");
			_state.OnGameEvents        += e => Debug.Log($"[Probe] events x{e.Length}");
			_state.OnRoundOver         += w => Debug.Log($"[Probe] round_over winner={w}");
			_state.OnGameOver          += w => Debug.Log($"[Probe] game_over winner={w}");
			_state.OnPlayerLeft        += id => Debug.Log($"[Probe] player_left {id}");
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
			GameStateLocator.Clear();
			_state?.Dispose();
			_net?.Dispose();
		}
	}
}
