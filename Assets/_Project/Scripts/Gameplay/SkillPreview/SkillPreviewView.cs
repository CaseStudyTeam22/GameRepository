using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Core.Skills;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay.SkillPreview
{
	/// <summary>
	/// </summary>
	public class SkillPreviewView : MonoBehaviour
	{
		[Header("Fallback (used when player's SkillSet is null or entry's cellPrefab is null)")]
		[SerializeField] SkillDefinition fallbackSkillSet;
		[SerializeField] GameObject fallbackCellPrefab;

		[Header("Layout")]
		[SerializeField] float yOffset = 0.06f;
		[SerializeField, Range(0f, 1f)] float opacity = 0.45f;
		[SerializeField, Tooltip("cell が tile を占める比率（1.0 = 完全に埋める; 0.85 = 隙間を少し残す）")]
		float cellTileFraction = 0.95f;
		[SerializeField, Tooltip("power 段階ごとの不透明度係数（インデックス 0/1/2 が power 1/2/3 に対応）")]
		float[] powerAlphaScale = { 0.5f, 0.75f, 1f };

		IGameState _state;
		IBoardCoords _board;

		readonly Dictionary<GameObject, List<GameObject>> _pools = new();
		readonly List<GameObject> _activeCells = new();

		void Start()
		{
			_state = GameStateLocator.Current;
			_board = BoardCoordsLocator.Current;
			if (_state == null || _board == null)
			{
				Debug.LogError("[SkillPreview] Locator not ready");
				return;
			}
			LocalIntentBus.OnChanged += Refresh;
			_state.OnPlayersChanged += Refresh;
			Refresh();
		}

		void OnDestroy()
		{
			LocalIntentBus.OnChanged -= Refresh;
			if (_state != null) _state.OnPlayersChanged -= Refresh;
		}

		void Refresh()
		{
			HideAll();
			if (_state == null || _board == null) return;

			var me = _state.Me;
			if (me == null) return;

			var intent = LocalIntentBus.Current;
			if (!intent.IsActive) return;

			var skillSet = ResolveSkillSet();
			if (skillSet == null) return;

			var entry = skillSet.GetEntry(intent.Mode);
			if (entry == null) return;

			var pattern = SkillPatternRegistry.Get(entry.patternType);
			if (pattern == null)
			{
				Debug.LogWarning($"[SkillPreview] No pattern impl for {entry.patternType}");
				return;
			}

			var cellPrefab = entry.cellPrefabOverride != null ? entry.cellPrefabOverride : fallbackCellPrefab;
			if (cellPrefab == null)
			{
				Debug.LogWarning("[SkillPreview] No cell prefab (entry override + fallback both null)");
				return;
			}

			bool useAlphaScale = entry.patternType == SkillPatternType.LineByPower;
			var color = ResolveColor(entry, me, intent.Power, useAlphaScale);

			foreach (var (gx, gy) in pattern.ResolveCells(intent, me))
				Show(gx, gy, color, cellPrefab);
		}

		SkillDefinition ResolveSkillSet()
		{
			var localPlayer = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.LocalPlayer : null;
			if (localPlayer != null && localPlayer.SkillSet != null) return localPlayer.SkillSet;
			return fallbackSkillSet;
		}

		Color ResolveColor(SkillEntry entry, PlayerDto me, int power, bool useAlphaScale)
		{
			Color baseColor = entry.tintOverride.a > 0.001f
				? entry.tintOverride
				: ParseColor(me.Color);

			float alpha = useAlphaScale ? opacity * GetPowerAlphaScale(power) : opacity;
			return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
		}

		float GetPowerAlphaScale(int power)
		{
			if (powerAlphaScale == null || powerAlphaScale.Length == 0) return 1f;
			int idx = Mathf.Clamp(power - 1, 0, powerAlphaScale.Length - 1);
			return powerAlphaScale[idx];
		}

		void Show(int gx, int gy, Color color, GameObject cellPrefab)
		{
			int n = _board.GridSize;
			if (gx < 0 || gx >= n || gy < 0 || gy >= n) return;

			var cell = AcquireCell(cellPrefab);
			cell.transform.position = _board.GridToWorld(gx, gy) + Vector3.up * yOffset;

			float targetMeters = _board.TileSize * cellTileFraction;
			float scale = ResolveAutoScale(cell, targetMeters);
			cell.transform.localScale = new Vector3(scale, scale, cell.transform.localScale.z);

			ApplyColor(cell, color);
			_activeCells.Add(cell);
		}

		static float ResolveAutoScale(GameObject cell, float targetMeters)
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

		GameObject AcquireCell(GameObject prefab)
		{
			if (!_pools.TryGetValue(prefab, out var pool))
			{
				pool = new List<GameObject>();
				_pools[prefab] = pool;
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

		static void ApplyColor(GameObject cell, Color color)
		{
			var sr = cell.GetComponentInChildren<SpriteRenderer>();
			if (sr != null) { sr.color = color; return; }
			var r = cell.GetComponentInChildren<Renderer>();
			if (r != null && r.material != null) r.material.color = color;
		}

		void HideAll()
		{
			for (int i = 0; i < _activeCells.Count; i++) _activeCells[i].SetActive(false);
			_activeCells.Clear();
		}

		static Color ParseColor(string hex)
		{
			return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
		}
	}
}
