using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	public static class WhiteSprite
	{
		private static Sprite s_Cached;

		public static Sprite Get()
		{
			if (s_Cached != null) return s_Cached;
			var tex = Texture2D.whiteTexture;
			s_Cached = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
			s_Cached.name = "WhiteUnit";
			return s_Cached;
		}
	}
}
