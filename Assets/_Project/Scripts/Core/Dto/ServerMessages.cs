using System.Collections.Generic;
using Newtonsoft.Json;

namespace GamblingAction.Core.Dto
{
	public class InitMessage
	{
		[JsonProperty("id")]       public string Id;
		[JsonProperty("players")]  public Dictionary<string, PlayerDto> Players;
		[JsonProperty("gridSize")] public int GridSize;
	}

	public class SyncStateMessage
	{
		[JsonProperty("players")] public Dictionary<string, PlayerDto> Players;
	}

	public class BeatMessage
	{
		[JsonProperty("beat")]       public int Beat;
		[JsonProperty("timeLeft")]   public int TimeLeft;
		[JsonProperty("gameActive")] public bool GameActive;
	}

	public class RoundOverMessage
	{
		[JsonProperty("winnerRole")] public string WinnerRole;
	}

	public class GameOverMessage
	{
		[JsonProperty("winnerRole")] public string WinnerRole;
	}

	public class WaitingForOthersMessage
	{
		[JsonProperty("waitingFor")] public string WaitingFor;
	}

	public static class ServerEvents
	{
		public const string Init                 = "init";
		public const string SyncState            = "sync_state";
		public const string SyncItems            = "sync_items";
		public const string Beat                 = "beat";
		public const string GameEvents           = "game_events";
		public const string StartExchange        = "start_exchange";
		public const string StartBuffSelection   = "start_buff_selection";
		public const string StartMatchCountdown  = "start_match_countdown";
		public const string RoundStart           = "round_start";
		public const string RoundOver            = "round_over";
		public const string GameOver             = "game_over";
		public const string WaitingForOthers     = "waiting_for_others";
		public const string PlayerLeft           = "player_left";
		public const string CloseAll             = "close_all";
		// Lobby で双方準備完了後のカウントダウン開始 / 中断。
		public const string StartCountdown       = "start_countdown";
		public const string CountdownCanceled    = "countdown_canceled";
		// 本轮の開始要求。クライアントは盤面・キャラ生成を終えてから round_ready を返す。
		public const string PrepareRound         = "prepare_round";
	}
}
