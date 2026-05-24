using GamblingAction.Domain;
using GamblingAction.Net;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Bootstrap
{
	public class GameInstaller : MonoBehaviour
	{
		public static GameInstaller Instance { get; private set; }

		// Bootstrap を経由せず単一シーンを直接 Play したときのデバッグ用。
		// DebugBootstrapper が true にすると、接続後に自動で AI 準備する。
		public static bool DebugAutoAIReady = false;

		[FormerlySerializedAs("serverUrl")]
		[SerializeField] private string m_ServerUrl = "http://localhost:3000";
		[FormerlySerializedAs("verboseProbeLogs")]
		[SerializeField] private bool m_VerboseProbeLogs = true;
		[FormerlySerializedAs("autoReady")]
		[SerializeField] private bool m_AutoReady = false;
		[FormerlySerializedAs("autoReadyAsAI")]
		[SerializeField] private bool m_AutoReadyAsAI = false;
		[FormerlySerializedAs("autoExchange")]
		[SerializeField] private bool m_AutoExchange = false;
		[FormerlySerializedAs("autoExchangeAmount")]
		[SerializeField] private int m_AutoExchangeAmount = 10;
		[FormerlySerializedAs("autoBuff")]
		[SerializeField] private bool m_AutoBuff = false;
		[FormerlySerializedAs("autoBuffId")]
		[SerializeField] private string m_AutoBuffId = "low_risk";

		private SocketIONetClient m_Net;
		private GameState m_State;

		public IGameState State => m_State;
		public INetClient Net => m_Net;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			// シーン遷移後も接続と状態を保持するため常駐させる。
			DontDestroyOnLoad(gameObject);

			m_Net = new SocketIONetClient();
			m_State = new GameState(m_Net);
			GameStateLocator.Set(m_State);

			if (m_VerboseProbeLogs)
				AttachProbe();

			// デバッグ直起動時は強制的に AI 準備する。
			bool autoReady = m_AutoReady || DebugAutoAIReady;
			bool autoReadyAsAI = m_AutoReadyAsAI || DebugAutoAIReady;

			if (autoReady)
				m_State.OnStateInitialized += () => m_State.SubmitReady(isAI: autoReadyAsAI);

			m_State.OnPhaseChanged += phase =>
			{
				if (autoReadyAsAI) return;

				if (m_AutoExchange && phase == EGamePhase.Exchange)
					m_State.SubmitExchange(m_AutoExchangeAmount);

				if (m_AutoBuff && phase == EGamePhase.BuffSelection)
					m_State.SubmitBuff(m_AutoBuffId);
			};
		}

		private void Start()
		{
			m_Net.Connect(m_ServerUrl);
		}

		private void AttachProbe()
		{
			m_State.OnConnectionChanged += c => Debug.Log($"[Probe] connection={c}");
			m_State.OnStateInitialized  += () => Debug.Log($"[Probe] init MyId={m_State.MyId} Players={m_State.Players.Count}");
			m_State.OnPhaseChanged      += p => Debug.Log($"[Probe] phase={p}");
			m_State.OnBeatChanged       += () => Debug.Log($"[Probe] beat={m_State.CurrentBeat} t={m_State.TimeLeft} active={m_State.GameActive}");
			m_State.OnGameEvents        += e => Debug.Log($"[Probe] events x{e.Length}");
			m_State.OnRoundOver         += w => Debug.Log($"[Probe] round_over winner={w}");
			m_State.OnGameOver          += w => Debug.Log($"[Probe] game_over winner={w}");
			m_State.OnPlayerLeft        += id => Debug.Log($"[Probe] player_left {id}");
		}

		private void OnDestroy()
		{
			// 自分が常駐インスタンスのときだけ後始末する。
			// 重複生成された側が自滅するときに、生きている方の状態と接続を壊さないため。
			if (Instance != this) return;
			Instance = null;
			GameStateLocator.Clear();
			m_State?.Dispose();
			m_Net?.Dispose();
		}
	}
}
