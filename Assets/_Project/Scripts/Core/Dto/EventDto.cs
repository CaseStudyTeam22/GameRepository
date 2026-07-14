using Newtonsoft.Json;

namespace GamblingAction.Core.Dto
{
	public class EventDto
	{
		[JsonProperty("type")]     public string Type;

		// サーバは衝突中点などで小数座標（例: 5.5）を送るため float で受ける
		[JsonProperty("x")]        public float? X;
		[JsonProperty("y")]        public float? Y;

		[JsonProperty("players")]  public string[] Players;

		[JsonProperty("vfxType")]  public string VfxType;
		[JsonProperty("targetId")] public string TargetId;
		[JsonProperty("dir")]      public string Dir;
		[JsonProperty("power")]    public int? Power;
		[JsonProperty("text")]     public string Text;

		[JsonProperty("dist")]     public int? Dist;
		[JsonProperty("damage")]   public int? Damage;
	}

	public static class EventTypes
	{
		public const string ClashExplosion = "clash_explosion";
		public const string ClashMoment    = "clash_moment";
		public const string Vfx            = "vfx";
		public const string Pushed         = "pushed";
		public const string Hit            = "hit";
	}

	public static class VfxTypes
	{
		public const string PushVfx    = "push_vfx";
		public const string AttackVfx  = "attack_vfx";
		public const string RestVfx    = "rest_vfx";
		public const string DefenseVfx = "defense_vfx";
		public const string Bump       = "bump";
	}
}
