using Newtonsoft.Json;

namespace GamblingAction.Core.Dto
{
	public class PlayerReadyMessage
	{
		[JsonProperty("isAI")] public bool IsAI;
	}

	public class ExchangeChipsMessage
	{
		[JsonProperty("amount")] public int Amount;
	}

	public class BuffSelectedMessage
	{
		[JsonProperty("buffId")] public string BuffId;
	}

	public class SelectCharaMessage
	{
		[JsonProperty("index")] public int Index;
	}

	public class SetIntentMessage
	{
		[JsonProperty("type")]  public string Type;
		[JsonProperty("dir")]   public string Dir;
		[JsonProperty("power")] public int Power;
	}

	public static class ClientEvents
	{
		public const string PlayerReady    = "player_ready";
		public const string PlayerUnready  = "player_unready";
		public const string EnterLobby     = "enter_lobby";
		public const string ExchangeChips  = "exchange_chips";
		public const string BuffSelected   = "buff_selected";
		public const string MissionCompleted = "mission_completed";
		// 盤面・キャラ生成を終え、ラウンドを始められる状態になったことを通知する。
		public const string RoundReady     = "round_ready";
		// Lobby でのキャラ選択。対局開始前に変更できる。
		public const string SelectChara    = "select_chara";
		public const string SetIntent      = "set_intent";
		public const string Shutdown       = "shutdown";
	}

	public static class BuffIds
	{
		public const string HighRisk = "high_risk";
		public const string LowRisk  = "low_risk";
	}

	public static class IntentTypes
	{
		public const string Move    = "move";
		public const string Push    = "push";
		public const string Attack  = "attack";
		public const string Defense = "defense";
		public const string Rest    = "rest";
		public const string None    = "none";
	}

	public static class Directions
	{
		public const string Up    = "up";
		public const string Down  = "down";
		public const string Left  = "left";
		public const string Right = "right";
	}

	public static class Roles
	{
		public const string P1 = "P1";
		public const string P2 = "P2";
	}
}
