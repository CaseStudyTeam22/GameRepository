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

	public class SetIntentMessage
	{
		[JsonProperty("type")]  public string Type;
		[JsonProperty("dir")]   public string Dir;
		[JsonProperty("power")] public int Power;
	}

	public static class ClientEvents
	{
		public const string PlayerReady    = "player_ready";
		public const string ExchangeChips  = "exchange_chips";
		public const string BuffSelected   = "buff_selected";
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
