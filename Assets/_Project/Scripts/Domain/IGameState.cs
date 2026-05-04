using System;
using System.Collections.Generic;
using GamblingAction.Core.Dto;

namespace GamblingAction.Domain
{
	public interface IGameState
	{
		string MyId { get; }
		int GridSize { get; }
		IReadOnlyDictionary<string, PlayerDto> Players { get; }
		IReadOnlyList<ItemDto> Items { get; }
		int CurrentBeat { get; }
		int TimeLeft { get; }
		bool GameActive { get; }
		GamePhase Phase { get; }
		bool IsConnected { get; }

		PlayerDto Me { get; }
		PlayerDto Opponent { get; }

		void SubmitIntent(string type, string dir, int power);
		void SubmitReady(bool isAI);
		void SubmitExchange(int amount);
		void SubmitBuff(string buffId);

		event Action OnStateInitialized;
		event Action OnPlayersChanged;
		event Action OnItemsChanged;
		event Action OnBeatChanged;
		event Action<EventDto[]> OnGameEvents;
		event Action<GamePhase> OnPhaseChanged;
		event Action<string> OnRoundOver;
		event Action<string> OnGameOver;
		event Action<string> OnPlayerLeft;
		event Action<string> OnWaitingForOthers;
		event Action<bool> OnConnectionChanged;
	}
}
