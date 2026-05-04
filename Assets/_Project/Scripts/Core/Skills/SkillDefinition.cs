using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingAction.Core.Skills
{
	/// <summary>
	/// </summary>
	[CreateAssetMenu(fileName = "SkillSet", menuName = "GamblingAction/Skill Set")]
	public class SkillDefinition : ScriptableObject
	{
		public List<SkillEntry> skills = new();

		public SkillEntry GetEntry(string skillType)
		{
			foreach (var s in skills)
				if (s.skillType == skillType) return s;
			return null;
		}
	}

	public enum SkillPatternType
	{
		LineByPower,
		Self,
		ForwardOne
	}

	[Serializable]
	public class SkillEntry
	{
		[Tooltip("プロトコル上の type（move/push/attack/defense/rest）")]
		public string skillType;

		[Tooltip("マス計算ルール。同一の skillType でもキャラクターごとに別の pattern を持てる。")]
		public SkillPatternType patternType;

		[Tooltip("プレビュー用の cell prefab。null = SkillPreviewView の fallbackCellPrefab を使用")]
		public GameObject cellPrefabOverride;

		[Tooltip("色のオーバーライド。透明（alpha=0）= プレイヤーカラー me.Color を使用")]
		public Color tintOverride = new(0f, 0f, 0f, 0f);
	}
}
