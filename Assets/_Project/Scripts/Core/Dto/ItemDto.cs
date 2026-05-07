using Newtonsoft.Json;

namespace GamblingAction.Core.Dto
{
	public class ItemDto
	{
		[JsonProperty("id")]   public double Id;
		[JsonProperty("type")] public string Type;
		[JsonProperty("x")]    public int X;
		[JsonProperty("y")]    public int Y;
	}
}
