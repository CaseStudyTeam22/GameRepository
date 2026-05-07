namespace GamblingAction.Domain
{
	public static class GameStateLocator
	{
		public static IGameState Current { get; private set; }

		public static void Set(IGameState state) => Current = state;
		public static void Clear() => Current = null;
	}
}
