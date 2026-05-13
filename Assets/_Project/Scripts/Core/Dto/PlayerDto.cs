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
		[JsonProperty("maxStamina")]  public int MaxStamina; // GameConfigからこっちに移行かな
		[JsonProperty("isAI")]        public bool IsAI;
		[JsonProperty("personality")] public string Personality;
		[JsonProperty("color")]       public string Color;
		[JsonProperty("selectedBuff")] public string SelectedBuff;
		[JsonProperty("buffReady")]   public bool BuffReady;
		[JsonProperty("falling")]     public bool Falling;

		[JsonProperty("buffEffects")] public List<BuffEffectDto> BuffEffects;
		// ここをModifierContainerにして、バフの種類ごとに持たせる形になるんだが、
		// listは使えそう
	}

	public class IntentDto
	{
		[JsonProperty("type")]  public string Type;
		[JsonProperty("dir")]   public string Dir;
		[JsonProperty("power")] public int Power;
	}

	// stats乗算
	// バフごとに持たないといけないため形状は変わるかも
	public class BuffEffectDto
	{
		[JsonProperty("type")] public string Type;
		[JsonProperty("value")] public float Value;
	}

	public enum EMissionType
	{
		Move,
		Push,
		//Attack,
		Defend,
		//Heal,
		GainCoin,
		GainChip
	}

	public class MissionDto
	{
		public EMissionType Type;
    	public int TargetCount;
    	public int CurrentCount;
    	public bool IsCleared;
	}
}
