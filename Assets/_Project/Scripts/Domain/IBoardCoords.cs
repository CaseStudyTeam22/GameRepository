using UnityEngine;

namespace GamblingAction.Domain
{
	public interface IBoardCoords
	{
		Vector3 GridToWorld(int x, int y);
		bool TryWorldToGrid(Vector3 world, out int x, out int y);
		float TileSize { get; }
		int GridSize { get; }
	}

	public static class BoardCoordsLocator
	{
		public static IBoardCoords Current { get; private set; }

		public static void Set(IBoardCoords board) => Current = board;
		public static void Clear() => Current = null;
	}
}
