using System;
using DG.Tweening;
using GamblingAction.Core;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	[ExecuteAlways]
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

		[Header("Intro Animation")]
		[Tooltip("登場演出を再生する。false ならマスは即座に最終状態で並ぶ")]
		[SerializeField] private bool m_PlayIntro = true;
		[Tooltip("各マスの落下開始高さ（目標位置からの上方オフセット）")]
		[SerializeField] private float m_IntroDropHeight = 5f;
		[Tooltip("各マス 1 枚の登場にかかる長さ（秒）")]
		[SerializeField] private float m_IntroTileDuration = 0.4f;
		[Tooltip("発生源からの距離 1（マス）あたりの遅延（秒）")]
		[SerializeField] private float m_IntroDelayPerDistance = 0.12f;
		[Tooltip("各マスの遅延に加える乱数の幅（秒）。0 で乱数なし")]
		[SerializeField] private float m_IntroDelayJitter = 0f;
		[Tooltip("落下と拡大に使うイージング")]
		[SerializeField] private Ease m_IntroEase = Ease.OutBack;
		[Tooltip("波及の発生源（ワールド座標）。各マスはここからの距離で遅延が決まる。Y は無視（XZ 平面距離）")]
		[SerializeField] private Vector3 m_IntroCenter = Vector3.zero;

		// 値が 0 以下のとき用の既定値。
		private const float k_DefaultTileDuration = 0.35f;
		private const float k_DefaultDelayPerDistance = 0.1f;

		public float TileSize => m_TileSize;
		public int GridSize => GameConfig.GridSize;

		// 登場演出が完了して盤面が確定したか。完了後に登録したハンドラは即時発火する。
		public bool IsBoardReady { get; private set; }
		public event Action OnBoardReady;

		private Sequence m_IntroSeq;
		private bool m_Generated;
		// 生成したマス全体の親。ClearBoard で破棄するために保持する。
		private Transform m_TilesRoot;

		private void Awake()
		{
			// エディタ非実行時は静的状態を汚さず、プレビューだけを扱う。
			if (!Application.isPlaying) return;

			Instance = this;
			BoardCoordsLocator.Set(this);
			if (m_GenerateTilesOnAwake) GenerateBoard();
		}

		// マスの生成と登場演出の外部トリガ。m_GenerateTilesOnAwake を false にし、
		// Lobby 開始や段階遷移など任意のタイミングから呼ぶ。二重生成はしない。
		public void GenerateBoard()
		{
			if (m_Generated) return;
			m_Generated = true;
			GenerateTiles();
		}

		// 生成済みのマスを破棄し、再生成できる状態へ戻す。ラウンド間で地形を作り直すために使う。
		public void ClearBoard()
		{
			m_IntroSeq?.Kill();
			m_IntroSeq = null;
			if (m_TilesRoot != null)
			{
				Destroy(m_TilesRoot.gameObject);
				m_TilesRoot = null;
			}
			m_Generated = false;
			IsBoardReady = false;
		}

		private void OnDestroy()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				ClearEditorPreview();
				return;
			}
#endif
			m_IntroSeq?.Kill();
			if (Instance == this) Instance = null;
			if (BoardCoordsLocator.Current == (IBoardCoords)this)
				BoardCoordsLocator.Clear();
		}

		// 盤面が確定したら登録する。既に確定済みなら即時発火する。
		public void AddBoardReadyHandler(Action handler)
		{
			if (handler == null) return;
			if (IsBoardReady) handler();
			else OnBoardReady += handler;
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
			m_TilesRoot = root;

			bool animate = m_PlayIntro && Application.isPlaying;
			if (animate) m_IntroSeq = DOTween.Sequence();

			float tileDuration = m_IntroTileDuration > 0f ? m_IntroTileDuration : k_DefaultTileDuration;
			float delayPerDistance = m_IntroDelayPerDistance > 0f ? m_IntroDelayPerDistance : k_DefaultDelayPerDistance;

			for (int y = 0; y < GridSize; y++)
			for (int x = 0; x < GridSize; x++)
			{
				var prefab = ((x + y) & 1) == 0 ? m_TilePrefabLight : m_TilePrefabDark;
				var tile = Instantiate(prefab, root);
				tile.name = $"Tile_{x}_{y}";

				Vector3 target = GridToWorld(x, y);
				if (!animate)
				{
					tile.transform.position = target;
					continue;
				}

				// 初期状態：目標位置の上方・スケール 0
				var t = tile.transform;
				t.position = target + Vector3.up * m_IntroDropHeight;
				t.localScale = Vector3.zero;

				// XZ 平面での発生源からの距離（Y は無視）
				float dx = target.x - m_IntroCenter.x;
				float dz = target.z - m_IntroCenter.z;
				float dist = Mathf.Sqrt(dx * dx + dz * dz);
				float delay = dist * delayPerDistance
					+ (m_IntroDelayJitter > 0f ? UnityEngine.Random.Range(0f, m_IntroDelayJitter) : 0f);

				m_IntroSeq.Insert(delay, t.DOMove(target, tileDuration).SetEase(m_IntroEase));
				m_IntroSeq.Insert(delay, t.DOScale(Vector3.one, tileDuration).SetEase(m_IntroEase));
			}

			if (animate)
				m_IntroSeq.OnComplete(MarkBoardReady);
			else
				MarkBoardReady();
		}

		private void MarkBoardReady()
		{
			IsBoardReady = true;
			OnBoardReady?.Invoke();
		}

#if UNITY_EDITOR
		private const string k_EditorPreviewRootName = "TilesPreview (Editor Only)";
		private Transform m_EditorPreviewRoot;

		private void OnEnable()
		{
			if (!Application.isPlaying) ScheduleEditorPreviewRebuild();
		}

		private void OnDisable()
		{
			// 実行開始・コンポーネント無効化・ドメインリロード時にプレビューを片付ける。
			if (!Application.isPlaying) ClearEditorPreview();
		}

		private void OnValidate()
		{
			if (!Application.isPlaying) ScheduleEditorPreviewRebuild();
		}

		// Instantiate/Destroy は OnValidate 等から直接呼べないため、安全なタイミングまで遅延する。
		private void ScheduleEditorPreviewRebuild()
		{
			UnityEditor.EditorApplication.delayCall += () =>
			{
				if (this == null) return;
				if (Application.isPlaying) return;
				RebuildEditorPreview();
			};
		}

		// エディタ非実行時に最終状態のマスをプレビュー生成する。HideAndDontSave のためシーンには保存されない。
		private void RebuildEditorPreview()
		{
			ClearEditorPreview();
			if (m_TilePrefabLight == null || m_TilePrefabDark == null) return;

			m_EditorPreviewRoot = new GameObject(k_EditorPreviewRootName).transform;
			m_EditorPreviewRoot.SetParent(transform, false);
			m_EditorPreviewRoot.gameObject.hideFlags = HideFlags.HideAndDontSave;

			for (int y = 0; y < GridSize; y++)
			for (int x = 0; x < GridSize; x++)
			{
				var prefab = ((x + y) & 1) == 0 ? m_TilePrefabLight : m_TilePrefabDark;
				var tile = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, m_EditorPreviewRoot);
				tile.transform.position = GridToWorld(x, y);
				tile.hideFlags = HideFlags.HideAndDontSave;
			}
		}

		private void ClearEditorPreview()
		{
			if (m_EditorPreviewRoot != null)
			{
				DestroyImmediate(m_EditorPreviewRoot.gameObject);
				m_EditorPreviewRoot = null;
				return;
			}
			// 参照を失った（ドメインリロード等）場合に名前で残骸を回収する。
			var existing = transform.Find(k_EditorPreviewRootName);
			if (existing != null) DestroyImmediate(existing.gameObject);
		}
#endif

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
