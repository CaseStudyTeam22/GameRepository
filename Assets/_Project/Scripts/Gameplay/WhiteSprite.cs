using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	public static class WhiteSprite
	{
		static Sprite _cached;

		public static Sprite Get()
		{
			if (_cached != null) return _cached;
			var tex = Texture2D.whiteTexture;
			_cached = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
			_cached.name = "WhiteUnit";
			return _cached;
		}
	}
}
