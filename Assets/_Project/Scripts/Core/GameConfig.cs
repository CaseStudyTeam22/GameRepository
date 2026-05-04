namespace GamblingAction.Core
{
	public static class GameConfig
	{
		public const int GridSize           = 8;
		public const int BeatIntervalMs     = 375;
		public const int BeatsPerCycle      = 4;
		public const int GameDurationSec    = 150;

		public const int InitialMoney       = 10000;
		public const int InitialChips       = 10;
		public const int InitialStamina     = 5;
		public const int MaxStamina         = 5;

		public const int ItemSpawnInterval  = 2;
		public const int MaxItemsOnField    = 10;
		public const int ChipItemValue      = 5;
		public const int MoneyItemValue     = 500;

		public const int HighRiskBuffCost   = 15;
		public const int LowRiskBuffCost    = 5;
		public const int HighRiskMaxStamina = 4;

		public static readonly (int x, int y) P1Start = (1, 6);
		public static readonly (int x, int y) P2Start = (6, 1);
		public const string P1Color = "#00f2fe";
		public const string P2Color = "#ff4444";
	}
}
