using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay.SkillPreview
{
	public class GridCursorView : MonoBehaviour
	{
		[Header("Cell Prefab")]
		[SerializeField] private GameObject m_CellPrefab;

		[Header("Layout")]
		[SerializeField] private float m_YOffset = 0.1f;
		[SerializeField, Range(0f, 1f)] private float m_OpacityNormal = 0.2f;
		[SerializeField, Range(0f, 1f)] private float m_OpacityHover = 0.7f;
		[SerializeField, Range(0f, 1f)] private float m_OpacityConfirmed = 1f;

		[SerializeField] private float m_CellTileFraction = 0.95f;

		[Header("Colors")]
		[SerializeField] private Color m_WarningColor = new Color(0.9f, 0.1f, 0.1f);

		[Header("Tuning")]
		[SerializeField] private int[] m_PushCosts = { 3, 5, 9 };

		private IGameState m_State;
		private IBoardCoords m_Board;

		private readonly Dictionary<GameObject, List<GameObject>> m_Pools = new();
		private readonly List<GameObject> m_ActiveCells = new();

		private void Start()
		{
			m_State = GameStateLocator.Current;
			m_Board = BoardCoordsLocator.Current;
			LocalIntentBus.OnChanged += Refresh;
			if (m_State != null)
			{
				m_State.OnPlayersChanged += Refresh;
			}
			Refresh();
		}

		private void OnDestroy()
		{
			LocalIntentBus.OnChanged -= Refresh;
			if (m_State != null) m_State.OnPlayersChanged -= Refresh;
		}

		private void Refresh()
		{
			if (m_State == null)
			{
				m_State = GameStateLocator.Current;
				if (m_State != null) m_State.OnPlayersChanged += Refresh;
			}
			if (m_Board == null) m_Board = BoardCoordsLocator.Current;

			HideAll();
			if (m_State == null || m_Board == null)
			{
				Debug.LogWarning("[GridCursor] Refresh skipped: GameState or Board is null.");
				return;
			}

			var me = m_State.Me;
			if (me == null) return;

			var intent = LocalIntentBus.Current;
			Debug.Log($"[GridCursor] Refresh: active={intent.IsActive}, mode={intent.Mode}, hovered={intent.HoveredX},{intent.HoveredY}, confirmed={intent.IsConfirmed}, charaIndex={me.CharaIndex}");

			bool isFighterSkill = (intent.Mode == IntentTypes.Skill && me.CharaIndex == 3);
			if (!intent.IsActive || (intent.Mode != IntentTypes.Push && !isFighterSkill)) return;

			if (m_CellPrefab == null)
			{
				Debug.LogWarning("[GridCursor] No cell prefab assigned (m_CellPrefab is null)");
				return;
			}

			Color baseColor = ParseColor(me.Color);

			if (isFighterSkill)
			{
				Color normalColor = new Color(baseColor.r, baseColor.g, baseColor.b, m_OpacityNormal);
				int myX = me.X;
				int myY = me.Y;
				string currentDir = intent.Dir;
				if (string.IsNullOrEmpty(currentDir))
				{
					currentDir = (me.Role == "P2") ? "down" : "up";
				}

				int cx = myX;
				int cy = myY;
				switch (currentDir)
				{
					case "up":    cy -= 1; break;
					case "down":  cy += 1; break;
					case "left":  cx -= 1; break;
					case "right": cx += 1; break;
				}

				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						int tx = cx + dx;
						int ty = cy + dy;
						if (tx == myX && ty == myY) continue; // 自分自身は除外
						Show(tx, ty, normalColor, m_CellPrefab);
					}
				}
				return;
			}

			if (intent.IsConfirmed)
			{
				// 確定時：確定したマス（TargetX, TargetY）のみを表示
				if (intent.TargetX >= 0 && intent.TargetY >= 0)
				{
					Color color = new Color(baseColor.r, baseColor.g, baseColor.b, m_OpacityConfirmed);
					Show(intent.TargetX, intent.TargetY, color, m_CellPrefab);
				}
			}
			else
			{
				// 未確定時：行動可能範囲（十字1〜3マス）を表示
				Color normalColor = new Color(baseColor.r, baseColor.g, baseColor.b, m_OpacityNormal);
				
				// 現在向いている向きにのみハイライトを表示
				int myX = me.X;
				int myY = me.Y;
				string currentDir = intent.Dir;
				if (string.IsNullOrEmpty(currentDir))
				{
					currentDir = (me.Role == "P2") ? "down" : "up";
				}

				for (int dist = 1; dist <= 3; dist++)
				{
					int tx = myX;
					int ty = myY;
					switch (currentDir)
					{
						case "up":    ty -= dist; break;
						case "down":  ty += dist; break;
						case "left":  tx -= dist; break;
						case "right": tx += dist; break;
					}
					Show(tx, ty, normalColor, m_CellPrefab);
				}

				// 現在ホバー中のマスを強調表示
				if (intent.HoveredX >= 0 && intent.HoveredY >= 0)
				{
					// コスト不足かどうか判定
					int power = intent.Power;
					bool costWarning = false;
					if (power >= 1 && power <= m_PushCosts.Length)
					{
						int cost = m_PushCosts[power - 1];
						if (me.Chips < cost)
						{
							costWarning = true;
						}
					}

					Color hoverColor = costWarning
						? new Color(m_WarningColor.r, m_WarningColor.g, m_WarningColor.b, m_OpacityHover)
						: new Color(baseColor.r, baseColor.g, baseColor.b, m_OpacityHover);

					Show(intent.HoveredX, intent.HoveredY, hoverColor, m_CellPrefab);
				}
			}
		}

		private void Show(int gx, int gy, Color color, GameObject cellPrefab)
		{
			int n = m_Board.GridSize;
			if (gx < 0 || gx >= n || gy < 0 || gy >= n) return;

			var cell = AcquireCell(cellPrefab);
			cell.transform.position = m_Board.GridToWorld(gx, gy) + Vector3.up * m_YOffset;
			Debug.Log($"[GridCursor] Showing cell at grid=({gx}, {gy}), world={cell.transform.position}, active={cell.activeSelf}");

			float targetMeters = m_Board.TileSize * m_CellTileFraction;
			float scale = ResolveAutoScale(cell, targetMeters);
			cell.transform.localScale = new Vector3(scale, scale, cell.transform.localScale.z);

			ApplyColor(cell, color);
			m_ActiveCells.Add(cell);
		}

		private static float ResolveAutoScale(GameObject cell, float targetMeters)
		{
			var sr = cell.GetComponentInChildren<SpriteRenderer>();
			if (sr != null && sr.sprite != null)
			{
				float spriteSize = sr.sprite.bounds.size.x;
				if (spriteSize > 0.0001f) return targetMeters / spriteSize;
			}
			var mf = cell.GetComponentInChildren<MeshFilter>();
			if (mf != null && mf.sharedMesh != null)
			{
				float meshSize = mf.sharedMesh.bounds.size.x;
				if (meshSize > 0.0001f) return targetMeters / meshSize;
			}
			return targetMeters;
		}

		private GameObject AcquireCell(GameObject prefab)
		{
			if (!m_Pools.TryGetValue(prefab, out var pool))
			{
				pool = new List<GameObject>();
				m_Pools[prefab] = pool;
			}
			for (int i = 0; i < pool.Count; i++)
			{
				if (!pool[i].activeSelf)
				{
					pool[i].SetActive(true);
					return pool[i];
				}
			}
			var spawned = Instantiate(prefab, transform);
			spawned.name = $"{prefab.name}_{pool.Count}";
			pool.Add(spawned);
			return spawned;
		}

		private static void ApplyColor(GameObject cell, Color color)
		{
			var sr = cell.GetComponentInChildren<SpriteRenderer>();
			if (sr != null) { sr.color = color; return; }
			var r = cell.GetComponentInChildren<Renderer>();
			if (r != null && r.material != null) r.material.color = color;
		}

		private void HideAll()
		{
			for (int i = 0; i < m_ActiveCells.Count; i++) m_ActiveCells[i].SetActive(false);
			m_ActiveCells.Clear();
		}

		private static Color ParseColor(string hex)
		{
			return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
		}
	}
}
