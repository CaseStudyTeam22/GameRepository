using GamblingAction.Core;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	public class BoardView : MonoBehaviour, IBoardCoords
	{
		public static BoardView Instance { get; private set; }

		[Header("Layout")]
		[SerializeField] float tileSize = 1f;

		[Header("Tile Prefabs")]
		[Tooltip("明色マスの prefab。Pivot は上面中央（厚みは下方向に伸びる）")]
		[SerializeField] GameObject tilePrefabLight;
		[Tooltip("暗色マスの prefab。Pivot は同じく上面中央。")]
		[SerializeField] GameObject tilePrefabDark;

		[Header("Generation")]
		[SerializeField] bool generateTilesOnAwake = true;

		public float TileSize => tileSize;
		public int GridSize => GameConfig.GridSize;

		void Awake()
		{
			Instance = this;
			BoardCoordsLocator.Set(this);
			if (generateTilesOnAwake) GenerateTiles();
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
			if (BoardCoordsLocator.Current == (IBoardCoords)this)
				BoardCoordsLocator.Clear();
		}

		public Vector3 GridToWorld(int x, int y)
		{
			float originOffset = (GridSize - 1) * tileSize * 0.5f;
			return transform.position + new Vector3(
				x * tileSize - originOffset,
				0f,
				-(y * tileSize - originOffset)
			);
		}

		public bool TryWorldToGrid(Vector3 world, out int x, out int y)
		{
			float originOffset = (GridSize - 1) * tileSize * 0.5f;
			float fx = (world.x - transform.position.x + originOffset) / tileSize;
			float fy = -(world.z - transform.position.z - originOffset) / tileSize;
			x = Mathf.RoundToInt(fx);
			y = Mathf.RoundToInt(fy);
			return x >= 0 && x < GridSize && y >= 0 && y < GridSize;
		}

		void GenerateTiles()
		{
			if (tilePrefabLight == null || tilePrefabDark == null)
			{
				Debug.LogError("[BoardView] tilePrefabLight / tilePrefabDark not assigned");
				return;
			}

			var root = new GameObject("Tiles").transform;
			root.SetParent(transform, false);

			for (int y = 0; y < GridSize; y++)
			for (int x = 0; x < GridSize; x++)
			{
				var prefab = ((x + y) & 1) == 0 ? tilePrefabLight : tilePrefabDark;
				var tile = Instantiate(prefab, root);
				tile.name = $"Tile_{x}_{y}";
				tile.transform.position = GridToWorld(x, y);
			}
		}

		void OnDrawGizmos()
		{
			Gizmos.color = Color.yellow;
			int n = GameConfig.GridSize;
			for (int y = 0; y < n; y++)
			for (int x = 0; x < n; x++)
			{
				var c = GridToWorld(x, y);
				Gizmos.DrawWireCube(c, new Vector3(tileSize, 0.02f, tileSize));
			}
		}
	}
}
