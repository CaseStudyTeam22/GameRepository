using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Core.Skills;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay.SkillPreview
{
	/// <summary>
	/// </summary>
	public class SkillPreviewView : MonoBehaviour
	{
		[Header("Fallback (used when player's SkillSet is null or entry's cellPrefab is null)")]
		[FormerlySerializedAs("fallbackSkillSet")]
		[SerializeField] private SkillDefinition m_FallbackSkillSet;
		[FormerlySerializedAs("fallbackCellPrefab")]
		[SerializeField] private GameObject m_FallbackCellPrefab;

		[Header("Layout")]
		[FormerlySerializedAs("yOffset")]
		[SerializeField] private float m_YOffset = 0.06f;
		[FormerlySerializedAs("opacity")]
		[SerializeField, Range(0f, 1f)] private float m_Opacity = 0.45f;
		[FormerlySerializedAs("cellTileFraction")]
		[SerializeField, Tooltip("cell が tile を占める比率（1.0 = 完全に埋める; 0.85 = 隙間を少し残す）")]
		private float m_CellTileFraction = 0.95f;
		[FormerlySerializedAs("powerAlphaScale")]
		[SerializeField, Tooltip("power 段階ごとの不透明度係数（インデックス 0/1/2 が power 1/2/3 に対応）")]
		private float[] m_PowerAlphaScale = { 0.5f, 0.75f, 1f };

		private IGameState m_State;
		private IBoardCoords m_Board;

		private readonly Dictionary<GameObject, List<GameObject>> m_Pools = new();
		private readonly List<GameObject> m_ActiveCells = new();

		private void Start()
		{
			m_State = GameStateLocator.Current;
			m_Board = BoardCoordsLocator.Current;
			if (m_State == null || m_Board == null)
			{
				Debug.LogError("[SkillPreview] Locator not ready");
				return;
			}
			LocalIntentBus.OnChanged += Refresh;
			m_State.OnPlayersChanged += Refresh;
			Refresh();
		}

		private void OnDestroy()
		{
			LocalIntentBus.OnChanged -= Refresh;
			if (m_State != null) m_State.OnPlayersChanged -= Refresh;
		}

		private void Refresh()
		{
			HideAll();
			if (m_State == null || m_Board == null) return;

			var me = m_State.Me;
			if (me == null) return;

			var intent = LocalIntentBus.Current;
			if (!intent.IsActive) return;

			var skillSet = ResolveSkillSet();
			if (skillSet == null) return;

			var entry = skillSet.GetEntry(intent.Mode);
			if (entry == null) return;

			var pattern = SkillPatternRegistry.Get(entry.PatternType);
			if (pattern == null)
			{
				Debug.LogWarning($"[SkillPreview] No pattern impl for {entry.PatternType}");
				return;
			}

			var cellPrefab = entry.CellPrefabOverride != null ? entry.CellPrefabOverride : m_FallbackCellPrefab;
			if (cellPrefab == null)
			{
				Debug.LogWarning("[SkillPreview] No cell prefab (entry override + fallback both null)");
				return;
			}

			bool useAlphaScale = entry.PatternType == ESkillPatternType.LineByPower;
			var color = ResolveColor(entry, me, intent.Power, useAlphaScale);

			foreach (var (gx, gy) in pattern.ResolveCells(intent, me))
				Show(gx, gy, color, cellPrefab);
		}

		private SkillDefinition ResolveSkillSet()
		{
			var localPlayer = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
			if (localPlayer != null && localPlayer.SkillSet != null) return localPlayer.SkillSet;
			return m_FallbackSkillSet;
		}

		private Color ResolveColor(SkillEntry entry, PlayerDto me, int power, bool useAlphaScale)
		{
			Color baseColor = entry.TintOverride.a > 0.001f
				? entry.TintOverride
				: ParseColor(me.Color);

			float alpha = useAlphaScale ? m_Opacity * GetPowerAlphaScale(power) : m_Opacity;
			return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
		}

		private float GetPowerAlphaScale(int power)
		{
			if (m_PowerAlphaScale == null || m_PowerAlphaScale.Length == 0) return 1f;
			int idx = Mathf.Clamp(power - 1, 0, m_PowerAlphaScale.Length - 1);
			return m_PowerAlphaScale[idx];
		}

		private void Show(int gx, int gy, Color color, GameObject cellPrefab)
		{
			int n = m_Board.GridSize;
			if (gx < 0 || gx >= n || gy < 0 || gy >= n) return;

			var cell = AcquireCell(cellPrefab);
			cell.transform.position = m_Board.GridToWorld(gx, gy) + Vector3.up * m_YOffset;

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
