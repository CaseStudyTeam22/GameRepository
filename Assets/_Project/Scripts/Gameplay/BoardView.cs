using GamblingAction.Core;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	public class BoardView : MonoBehaviour, IBoardCoords
	{
		public static BoardView Instance { get; private set; }

		[Header("Layout")]
		[FormerlySerializedAs("tileSize")]
		[SerializeField] private float m_TileSize = 1f;

		[Header("Tile Prefabs")]
		[Tooltip("明色マスの prefab。Pivot は上面中央（厚みは下方向に伸びる）")]
		[FormerlySerializedAs("tilePrefabLight")]
		[SerializeField] private GameObject m_TilePrefabLight;
		[Tooltip("暗色マスの prefab。Pivot は同じく上面中央。")]
		[FormerlySerializedAs("tilePrefabDark")]
		[SerializeField] private GameObject m_TilePrefabDark;

		[Header("Generation")]
		[FormerlySerializedAs("generateTilesOnAwake")]
		[SerializeField] private bool m_GenerateTilesOnAwake = true;

		public float TileSize => m_TileSize;
		public int GridSize => GameConfig.GridSize;

		private void Awake()
		{
			Instance = this;
			BoardCoordsLocator.Set(this);
			if (m_GenerateTilesOnAwake) GenerateTiles();
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
			if (BoardCoordsLocator.Current == (IBoardCoords)this)
				BoardCoordsLocator.Clear();
		}

		public Vector3 GridToWorld(int x, int y)
		{
			float originOffset = (GridSize - 1) * m_TileSize * 0.5f;
			return transform.position + new Vector3(
				x * m_TileSize - originOffset,
				0f,
				-(y * m_TileSize - originOffset)
			);
		}

		public bool TryWorldToGrid(Vector3 world, out int x, out int y)
		{
			float originOffset = (GridSize - 1) * m_TileSize * 0.5f;
			float fx = (world.x - transform.position.x + originOffset) / m_TileSize;
			float fy = -(world.z - transform.position.z - originOffset) / m_TileSize;
			x = Mathf.RoundToInt(fx);
			y = Mathf.RoundToInt(fy);
			return x >= 0 && x < GridSize && y >= 0 && y < GridSize;
		}

		private void GenerateTiles()
		{
			if (m_TilePrefabLight == null || m_TilePrefabDark == null)
			{
				Debug.LogError("[BoardView] tilePrefabLight / tilePrefabDark not assigned");
				return;
			}

			var root = new GameObject("Tiles").transform;
			root.SetParent(transform, false);

			for (int y = 0; y < GridSize; y++)
			for (int x = 0; x < GridSize; x++)
			{
				var prefab = ((x + y) & 1) == 0 ? m_TilePrefabLight : m_TilePrefabDark;
				var tile = Instantiate(prefab, root);
				tile.name = $"Tile_{x}_{y}";
				tile.transform.position = GridToWorld(x, y);
			}
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.yellow;
			int n = GameConfig.GridSize;
			for (int y = 0; y < n; y++)
			for (int x = 0; x < n; x++)
			{
				var c = GridToWorld(x, y);
				Gizmos.DrawWireCube(c, new Vector3(m_TileSize, 0.02f, m_TileSize));
			}
		}
	}
}
