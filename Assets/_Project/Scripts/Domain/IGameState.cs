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
		EGamePhase Phase { get; }
		bool IsConnected { get; }

		PlayerDto Me { get; }
		PlayerDto Opponent { get; }

		void SubmitIntent(string type, string dir, int power);
		void SubmitReady(bool isAI);
		void SubmitUnready();
		void SubmitEnterLobby();
		void SubmitExchange(int amount);
		void SubmitBuff(string buffId);
		void SubmitMission(string missionId);
		void SubmitRoundReady();
		void SubmitSelectChara(int index);

		event Action OnStateInitialized;
		event Action OnPlayersChanged;
		event Action OnItemsChanged;
		event Action OnBeatChanged;
		event Action<EventDto[]> OnGameEvents;
		event Action<EGamePhase> OnPhaseChanged;
		event Action<string> OnRoundOver;
		event Action<string> OnGameOver;
		event Action<string> OnPlayerLeft;
		event Action<string> OnWaitingForOthers;
		event Action OnCountdownStart;
		event Action OnCountdownCancel;
		// サーバがラウンドの開始を要求。盤面・キャラ生成のトリガに使う。
		event Action OnPrepareRound;
		// 誰かがキャラを選択した（playerId, 立ち絵 index）。相手側の表示同期に使う。
		event Action<string, int> OnCharaSelected;
		event Action<bool> OnConnectionChanged;
	}
}
