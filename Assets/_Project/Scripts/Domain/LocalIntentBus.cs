using System;

namespace GamblingAction.Domain
{
	/// <summary>
	/// </summary>
	public class LocalIntent
	{
		public string Mode;   // null/none/move/push/attack/defense/rest
		public string Dir;    // up/down/left/right or null
		public int Power = 1;

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

		public static void Clear()
		{
			Current.Mode = null;
			Current.Dir = null;
			Current.Power = 1;
			OnChanged?.Invoke();
		}
	}
}
