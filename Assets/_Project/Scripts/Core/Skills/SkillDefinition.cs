using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Core.Skills
{
	/// <summary>
	/// </summary>
	[CreateAssetMenu(fileName = "SkillSet", menuName = "GamblingAction/Skill Set")]
	public class SkillDefinition : ScriptableObject
	{
		[FormerlySerializedAs("skills")]
		[SerializeField] private List<SkillEntry> m_Skills = new();

		public IReadOnlyList<SkillEntry> Skills => m_Skills;

		public SkillEntry GetEntry(string skillType)
		{
			foreach (var s in m_Skills)
				if (s.SkillType == skillType) return s;
			return null;
		}
	}

	public enum ESkillPatternType
	{
		LineByPower,
		Self,
		ForwardOne
	}

	[Serializable]
	public class SkillEntry
	{
		[FormerlySerializedAs("skillType")]
		[Tooltip("プロトコル上の type（move/push/attack/defense/skill）")]
		[SerializeField] private string m_SkillType;

		[FormerlySerializedAs("patternType")]
		[Tooltip("マス計算ルール。同一の skillType でもキャラクターごとに別の pattern を持てる。")]
		[SerializeField] private ESkillPatternType m_PatternType;

		[FormerlySerializedAs("cellPrefabOverride")]
		[Tooltip("プレビュー用の cell prefab。null = SkillPreviewView の fallbackCellPrefab を使用")]
		[SerializeField] private GameObject m_CellPrefabOverride;

		[FormerlySerializedAs("tintOverride")]
		[Tooltip("色のオーバーライド。透明（alpha=0）= プレイヤーカラー me.Color を使用")]
		[SerializeField] private Color m_TintOverride = new(0f, 0f, 0f, 0f);

		public string SkillType => m_SkillType;
		public ESkillPatternType PatternType => m_PatternType;
		public GameObject CellPrefabOverride => m_CellPrefabOverride;
		public Color TintOverride => m_TintOverride;
	}
}
