using System;

namespace GamblingAction.Domain
{
	/// <summary>
	/// </summary>
	public class LocalIntent
	{
		public string Mode { get; set; }
		public string Dir { get; set; }
		public int Power { get; set; } = 1;

		public int TargetX { get; set; } = -1;
		public int TargetY { get; set; } = -1;
		public int HoveredX { get; set; } = -1;
		public int HoveredY { get; set; } = -1;
		public bool IsConfirmed { get; set; }

		public bool IsActive => !string.IsNullOrEmpty(Mode) && Mode != "none";
	}

	public static class LocalIntentBus
	{
		public static LocalIntent Current { get; } = new LocalIntent();

		public static event Action OnChanged;

		public static void NotifyChanged() => OnChanged?.Invoke();

		public static void Set(string mode, string dir, int power)
		{
			Current.Mode = mode;
			Current.Dir = dir;
			Current.Power = power;
			OnChanged?.Invoke();
		}

		public static void Set(string mode, string dir, int power, int targetX, int targetY, int hoveredX, int hoveredY, bool isConfirmed)
		{
			Current.Mode = mode;
			Current.Dir = dir;
			Current.Power = power;
			Current.TargetX = targetX;
			Current.TargetY = targetY;
			Current.HoveredX = hoveredX;
			Current.HoveredY = hoveredY;
			Current.IsConfirmed = isConfirmed;
			OnChanged?.Invoke();
		}

		public static void Clear()
		{
			Current.Mode = null;
			Current.Dir = null;
			Current.Power = 1;
			Current.TargetX = -1;
			Current.TargetY = -1;
			Current.HoveredX = -1;
			Current.HoveredY = -1;
			Current.IsConfirmed = false;
			OnChanged?.Invoke();
		}
	}
}
