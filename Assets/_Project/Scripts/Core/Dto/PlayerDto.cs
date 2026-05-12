using Newtonsoft.Json;
using System.Collections.Generic;

namespace GamblingAction.Core.Dto
{
	public class PlayerDto
	{
		[JsonProperty("id")]          public string Id;
		[JsonProperty("role")]        public string Role;
		[JsonProperty("x")]           public int X;
		[JsonProperty("y")]           public int Y;
		[JsonProperty("intent")]      public IntentDto Intent;
		[JsonProperty("ready")]       public bool Ready;
		[JsonProperty("exchanged")]   public bool Exchanged;
		[JsonProperty("score")]       public int Score;
		[JsonProperty("money")]       public int Money;
		[JsonProperty("chips")]       public int Chips;
		[JsonProperty("stamina")]     public int Stamina;
		[JsonProperty("isAI")]        public bool IsAI;
		[JsonProperty("personality")] public string Personality;
		[JsonProperty("color")]       public string Color;
		[JsonProperty("selectedBuff")] public string SelectedBuff;
		[JsonProperty("buffReady")]   public bool BuffReady;
		[JsonProperty("falling")]     public bool Falling;

		[JsonProperty("buffEffects")] public List<BuffEffectDto> BuffEffects;
	}

	public class IntentDto
	{
		[JsonProperty("type")]  public string Type;
		[JsonProperty("dir")]   public string Dir;
		[JsonProperty("power")] public int Power;
	}

	public class BuffEffectDto
	{
		[JsonProperty("type")] public string Type;
		[JsonProperty("value")] public float Value;
	}
}
