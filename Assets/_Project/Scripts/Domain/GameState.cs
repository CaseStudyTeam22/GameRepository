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
		readonly INetClient _net;
		readonly Dictionary<string, PlayerDto> _players = new();
		List<ItemDto> _items = new();

		public string MyId { get; private set; }
		public int GridSize { get; private set; } = GamblingAction.Core.GameConfig.GridSize;
		public IReadOnlyDictionary<string, PlayerDto> Players => _players;
		public IReadOnlyList<ItemDto> Items => _items;
		public int CurrentBeat { get; private set; }
		public int TimeLeft { get; private set; }
		public bool GameActive { get; private set; }
		public GamePhase Phase { get; private set; } = GamePhase.Lobby;
		public bool IsConnected { get; private set; }

		public PlayerDto Me =>
			MyId != null && _players.TryGetValue(MyId, out var p) ? p : null;

		public PlayerDto Opponent =>
			_players.Values.FirstOrDefault(p => p.Id != MyId);

		public event Action OnStateInitialized;
		public event Action OnPlayersChanged;
		public event Action OnItemsChanged;
		public event Action OnBeatChanged;
		public event Action<EventDto[]> OnGameEvents;
		public event Action<GamePhase> OnPhaseChanged;
		public event Action<string> OnRoundOver;
		public event Action<string> OnGameOver;
		public event Action<string> OnPlayerLeft;
		public event Action<string> OnWaitingForOthers;
		public event Action<bool> OnConnectionChanged;

		public GameState(INetClient net)
		{
			_net = net;
			Subscribe();
		}

		public void SubmitIntent(string type, string dir, int power)
		{
			if (!GameActive || CurrentBeat >= 4) return;
			var me = Me;
			if (me == null || me.IsAI) return;
			_net.Emit(ClientEvents.SetIntent, new SetIntentMessage { Type = type, Dir = dir, Power = power });
		}

		public void SubmitReady(bool isAI)
		{
			_net.Emit(ClientEvents.PlayerReady, new PlayerReadyMessage { IsAI = isAI });
		}

		public void SubmitExchange(int amount)
		{
			_net.Emit(ClientEvents.ExchangeChips, new ExchangeChipsMessage { Amount = amount });
		}

		public void SubmitBuff(string buffId)
		{
			_net.Emit(ClientEvents.BuffSelected, new BuffSelectedMessage { BuffId = buffId });
		}

		void Subscribe()
		{
			_net.OnConnected += () =>
			{
				IsConnected = true;
				OnConnectionChanged?.Invoke(true);
			};
			_net.OnDisconnected += () =>
			{
				IsConnected = false;
				OnConnectionChanged?.Invoke(false);
			};

			_net.On<InitMessage>(ServerEvents.Init, HandleInit);
			_net.On<SyncStateMessage>(ServerEvents.SyncState, HandleSyncState);
			_net.On<ItemDto[]>(ServerEvents.SyncItems, HandleSyncItems);
			_net.On<BeatMessage>(ServerEvents.Beat, HandleBeat);
			_net.On<EventDto[]>(ServerEvents.GameEvents, HandleGameEvents);
			_net.On<RoundOverMessage>(ServerEvents.RoundOver, HandleRoundOver);
			_net.On<GameOverMessage>(ServerEvents.GameOver, HandleGameOver);
			_net.On<WaitingForOthersMessage>(ServerEvents.WaitingForOthers, HandleWaitingForOthers);
			_net.On<string>(ServerEvents.PlayerLeft, HandlePlayerLeft);

			_net.On(ServerEvents.StartExchange,       () => SetPhase(GamePhase.Exchange));
			_net.On(ServerEvents.StartBuffSelection,  () => SetPhase(GamePhase.BuffSelection));
			_net.On(ServerEvents.StartMatchCountdown, () => SetPhase(GamePhase.Countdown));
			_net.On(ServerEvents.RoundStart,          () => SetPhase(GamePhase.Battle));
			_net.On(ServerEvents.CloseAll,            HandleCloseAll);
		}

		void HandleInit(InitMessage msg)
		{
			MyId = msg.Id;
			GridSize = msg.GridSize;
			ReplacePlayers(msg.Players);
			Debug.Log($"[GameState] init: id={MyId} players={_players.Count} grid={GridSize}");
			OnStateInitialized?.Invoke();
			OnPlayersChanged?.Invoke();
		}

		void HandleSyncState(SyncStateMessage msg)
		{
			ReplacePlayers(msg.Players);
			OnPlayersChanged?.Invoke();
		}

		void HandleSyncItems(ItemDto[] items)
		{
			_items = items != null ? new List<ItemDto>(items) : new List<ItemDto>();
			OnItemsChanged?.Invoke();
		}

		void HandleBeat(BeatMessage msg)
		{
			CurrentBeat = msg.Beat;
			TimeLeft = msg.TimeLeft;
			GameActive = msg.GameActive;
			OnBeatChanged?.Invoke();
		}

		void HandleGameEvents(EventDto[] events)
		{
			if (events == null || events.Length == 0) return;
			OnGameEvents?.Invoke(events);
		}

		void HandleRoundOver(RoundOverMessage msg)
		{
			SetPhase(GamePhase.RoundOver);
			OnRoundOver?.Invoke(msg.WinnerRole);
		}

		void HandleGameOver(GameOverMessage msg)
		{
			SetPhase(GamePhase.GameOver);
			OnGameOver?.Invoke(msg.WinnerRole);
		}

		void HandleWaitingForOthers(WaitingForOthersMessage msg)
		{
			OnWaitingForOthers?.Invoke(msg.WaitingFor);
		}

		void HandlePlayerLeft(string id)
		{
			if (_players.Remove(id))
				OnPlayersChanged?.Invoke();
			OnPlayerLeft?.Invoke(id);
		}

		void HandleCloseAll()
		{
			Debug.LogWarning("[GameState] Server requested close_all");
			_net.Disconnect();
		}

		void ReplacePlayers(Dictionary<string, PlayerDto> incoming)
		{
			_players.Clear();
			if (incoming == null) return;
			foreach (var kv in incoming)
				_players[kv.Key] = kv.Value;
		}

		void SetPhase(GamePhase phase)
		{
			if (Phase == phase) return;
			Phase = phase;
			Debug.Log($"[GameState] phase → {phase}");
			OnPhaseChanged?.Invoke(phase);
		}

		public void Dispose()
		{
			_net.Off(ServerEvents.Init);
			_net.Off(ServerEvents.SyncState);
			_net.Off(ServerEvents.SyncItems);
			_net.Off(ServerEvents.Beat);
			_net.Off(ServerEvents.GameEvents);
			_net.Off(ServerEvents.RoundOver);
			_net.Off(ServerEvents.GameOver);
			_net.Off(ServerEvents.WaitingForOthers);
			_net.Off(ServerEvents.PlayerLeft);
			_net.Off(ServerEvents.StartExchange);
			_net.Off(ServerEvents.StartBuffSelection);
			_net.Off(ServerEvents.StartMatchCountdown);
			_net.Off(ServerEvents.RoundStart);
			_net.Off(ServerEvents.CloseAll);
		}
	}
}
