using System;
using System.Collections.Generic;
using System.Linq;
using GamblingAction.Core.Dto;
using GamblingAction.Net;
using UnityEngine;

namespace GamblingAction.Domain
{
	public class GameState : IGameState, IDisposable
	{
		private readonly INetClient m_Net;
		private readonly Dictionary<string, PlayerDto> m_Players = new();
		private List<ItemDto> m_Items = new();

		public string MyId { get; private set; }
		public int GridSize { get; private set; } = GamblingAction.Core.GameConfig.GridSize;
		public IReadOnlyDictionary<string, PlayerDto> Players => m_Players;
		public IReadOnlyList<ItemDto> Items => m_Items;
		public int CurrentBeat { get; private set; }
		public int TimeLeft { get; private set; }
		public bool GameActive { get; private set; }
		public EGamePhase Phase { get; private set; } = EGamePhase.Lobby;
		public bool IsConnected { get; private set; }

		public PlayerDto Me =>
			MyId != null && m_Players.TryGetValue(MyId, out var p) ? p : null;

		public PlayerDto Opponent =>
			m_Players.Values.FirstOrDefault(p => p.Id != MyId);

		public event Action OnStateInitialized;
		public event Action OnPlayersChanged;
		public event Action OnItemsChanged;
		public event Action OnBeatChanged;
		public event Action<EventDto[]> OnGameEvents;
		public event Action<EGamePhase> OnPhaseChanged;
		public event Action<string> OnRoundOver;
		public event Action<string> OnGameOver;
		public event Action<string> OnPlayerLeft;
		public event Action<string> OnWaitingForOthers;
		public event Action<bool> OnConnectionChanged;

		public GameState(INetClient net)
		{
			m_Net = net;
			Subscribe();
		}

		public void SubmitIntent(string type, string dir, int power)
		{
			if (!GameActive || CurrentBeat >= 4) return;
			var me = Me;
			if (me == null || me.IsAI) return;
			m_Net.Emit(ClientEvents.SetIntent, new SetIntentMessage { Type = type, Dir = dir, Power = power });
		}

		public void SubmitReady(bool isAI)
		{
			m_Net.Emit(ClientEvents.PlayerReady, new PlayerReadyMessage { IsAI = isAI });
		}

		public void SubmitExchange(int amount)
		{
			m_Net.Emit(ClientEvents.ExchangeChips, new ExchangeChipsMessage { Amount = amount });
		}

		public void SubmitBuff(string buffId)
		{
			m_Net.Emit(ClientEvents.BuffSelected, new BuffSelectedMessage { BuffId = buffId });
		}

		public int GetCalculatedPower(string playerId)
		{
			if (!m_Players.TryGetValue(playerId, out var player)) return 0;
			int power = player.IntentPower; // 多分これintent.powerじゃないか

			// ここでバフ量計算
			// foreach (var item in m_Items)
			// {
			// 	if (item.TargetPlayerId == playerId)
			// 		power += item.Power;
			// }
			return power;
		}

		private void Subscribe()
		{
			m_Net.OnConnected += () =>
			{
				IsConnected = true;
				OnConnectionChanged?.Invoke(true);
			};
			m_Net.OnDisconnected += () =>
			{
				IsConnected = false;
				OnConnectionChanged?.Invoke(false);
			};

			m_Net.On<InitMessage>(ServerEvents.Init, HandleInit);
			m_Net.On<SyncStateMessage>(ServerEvents.SyncState, HandleSyncState);
			m_Net.On<ItemDto[]>(ServerEvents.SyncItems, HandleSyncItems);
			m_Net.On<BeatMessage>(ServerEvents.Beat, HandleBeat);
			m_Net.On<EventDto[]>(ServerEvents.GameEvents, HandleGameEvents);
			m_Net.On<RoundOverMessage>(ServerEvents.RoundOver, HandleRoundOver);
			m_Net.On<GameOverMessage>(ServerEvents.GameOver, HandleGameOver);
			m_Net.On<WaitingForOthersMessage>(ServerEvents.WaitingForOthers, HandleWaitingForOthers);
			m_Net.On<string>(ServerEvents.PlayerLeft, HandlePlayerLeft);

			m_Net.On(ServerEvents.StartExchange,       () => SetPhase(EGamePhase.Exchange));
			m_Net.On(ServerEvents.StartBuffSelection,  () => SetPhase(EGamePhase.BuffSelection));
			m_Net.On(ServerEvents.StartMatchCountdown, () => SetPhase(EGamePhase.Countdown));
			m_Net.On(ServerEvents.RoundStart,          () => SetPhase(EGamePhase.Battle));
			m_Net.On(ServerEvents.CloseAll,            HandleCloseAll);
		}

		private void HandleInit(InitMessage msg)
		{
			MyId = msg.Id;
			GridSize = msg.GridSize;
			ReplacePlayers(msg.Players);
			Debug.Log($"[GameState] init: id={MyId} players={m_Players.Count} grid={GridSize}");
			OnStateInitialized?.Invoke();
			OnPlayersChanged?.Invoke();
		}

		private void HandleSyncState(SyncStateMessage msg)
		{
			ReplacePlayers(msg.Players);
			OnPlayersChanged?.Invoke();
		}

		private void HandleSyncItems(ItemDto[] items)
		{
			m_Items = items != null ? new List<ItemDto>(items) : new List<ItemDto>();
			OnItemsChanged?.Invoke();
		}

		private void HandleBeat(BeatMessage msg)
		{
			CurrentBeat = msg.Beat;
			TimeLeft = msg.TimeLeft;
			GameActive = msg.GameActive;
			OnBeatChanged?.Invoke();
		}

		private void HandleGameEvents(EventDto[] events)
		{
			// ここで特定のイベント(動いたならとか)で処理を実行等々

			if (events == null || events.Length == 0) return;
			OnGameEvents?.Invoke(events);
		}

		private void HandleRoundOver(RoundOverMessage msg)
		{
			SetPhase(EGamePhase.RoundOver);
			OnRoundOver?.Invoke(msg.WinnerRole);
		}

		private void HandleGameOver(GameOverMessage msg)
		{
			SetPhase(EGamePhase.GameOver);
			OnGameOver?.Invoke(msg.WinnerRole);
		}

		private void HandleWaitingForOthers(WaitingForOthersMessage msg)
		{
			OnWaitingForOthers?.Invoke(msg.WaitingFor);
		}

		private void HandlePlayerLeft(string id)
		{
			if (m_Players.Remove(id))
				OnPlayersChanged?.Invoke();
			OnPlayerLeft?.Invoke(id);
		}

		private void HandleCloseAll()
		{
			Debug.LogWarning("[GameState] Server requested close_all");
			m_Net.Disconnect();
		}

		private void ReplacePlayers(Dictionary<string, PlayerDto> incoming)
		{
			m_Players.Clear();
			if (incoming == null) return;
			foreach (var kv in incoming)
				m_Players[kv.Key] = kv.Value;
		}

		private void SetPhase(EGamePhase phase)
		{
			if (Phase == phase) return;
			Phase = phase;
			Debug.Log($"[GameState] phase → {phase}");
			OnPhaseChanged?.Invoke(phase);
		}

		public void Dispose()
		{
			m_Net.Off(ServerEvents.Init);
			m_Net.Off(ServerEvents.SyncState);
			m_Net.Off(ServerEvents.SyncItems);
			m_Net.Off(ServerEvents.Beat);
			m_Net.Off(ServerEvents.GameEvents);
			m_Net.Off(ServerEvents.RoundOver);
			m_Net.Off(ServerEvents.GameOver);
			m_Net.Off(ServerEvents.WaitingForOthers);
			m_Net.Off(ServerEvents.PlayerLeft);
			m_Net.Off(ServerEvents.StartExchange);
			m_Net.Off(ServerEvents.StartBuffSelection);
			m_Net.Off(ServerEvents.StartMatchCountdown);
			m_Net.Off(ServerEvents.RoundStart);
			m_Net.Off(ServerEvents.CloseAll);
		}
	}
}
