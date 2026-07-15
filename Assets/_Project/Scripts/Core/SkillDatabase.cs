using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingAction.Core
{
    /// <summary>
    /// スキルID をキーにして アイコン Sprite を管理する ScriptableObject。
    /// ロビーの選択画面とインゲームのスキルボタンの両方から参照する。
    /// スプレッドシートには画像パスを持たせず、スキルIDだけで紐付ける。
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "GamblingAction/SkillDatabase")]
    public class SkillDatabase : ScriptableObject
    {
        [Serializable]
        public class SkillEntry
        {
            [Tooltip("スキルID（例: heal_instant）。スプレッドシートの SkillId カラムと一致させる。")]
            public string SkillId;

            [Tooltip("スキルアイコン画像")]
            public Sprite Icon;
        }

        [SerializeField] private SkillEntry[] m_Entries;

        private Dictionary<string, Sprite> m_Cache;

        /// <summary>
        /// スキルIDに対応するアイコンを返す。登録がない場合は null を返す。
        /// </summary>
        public Sprite GetIcon(string skillId)
        {
            BuildCacheIfNeeded();
            if (string.IsNullOrEmpty(skillId)) return null;
            return m_Cache.TryGetValue(skillId, out var sprite) ? sprite : null;
        }

        private void BuildCacheIfNeeded()
        {
            if (m_Cache != null) return;
            m_Cache = new Dictionary<string, Sprite>();
            if (m_Entries == null) return;
            foreach (var entry in m_Entries)
            {
                if (string.IsNullOrEmpty(entry.SkillId) || entry.Icon == null) continue;
                m_Cache[entry.SkillId] = entry.Icon;
            }
        }

        // ScriptableObject が再インポートされた際にキャッシュをクリアする
        private void OnValidate() => m_Cache = null;
    }
}
