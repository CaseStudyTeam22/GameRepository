using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// 相手の選択したインテントをキャラクターの右上などに吹き出しアイコンで表示するビュー。
	/// イカサマスキル発動時などに使用される。
	/// </summary>
	public class OpponentIntentBubbleView : MonoBehaviour
	{
		[Header("UI / Visual elements")]
		[SerializeField, Tooltip("吹き出し全体のゲームオブジェクト（表示・非表示の切り替え用）")]
		private GameObject m_BubbleRoot;

		[SerializeField, Tooltip("コマンドアイコンの描画用SpriteRenderer")]
		private SpriteRenderer m_IconRenderer;

		[Header("Intent Icons")]
		[SerializeField, Tooltip("移動(move)時に表示するアイコン")]
		private Sprite m_MoveSprite;

		[SerializeField, Tooltip("押し出し(push)時に表示するアイコン")]
		private Sprite m_PushSprite;

		[SerializeField, Tooltip("攻撃(attack)時に表示するアイコン")]
		private Sprite m_AttackSprite;

		[SerializeField, Tooltip("防御(defense)時に表示するアイコン")]
		private Sprite m_DefenseSprite;

		[SerializeField, Tooltip("スキル(skill)時に表示するアイコン")]
		private Sprite m_SkillSprite;

		[SerializeField, Tooltip("休息(rest)時に表示するアイコン")]
		private Sprite m_RestSprite;

		private void Awake()
		{
			Hide();
		}

		/// <summary>
		/// 指定されたインテントタイプに応じた吹き出しを表示する。
		/// </summary>
		public void ShowIntent(string type)
		{
			if (m_BubbleRoot == null) return;

			Sprite sprite = GetSpriteForType(type);
			if (sprite != null)
			{
				if (m_IconRenderer != null)
				{
					m_IconRenderer.sprite = sprite;
				}
				m_BubbleRoot.SetActive(true);
			}
			else
			{
				Hide();
			}
		}

		/// <summary>
		/// 吹き出しを非表示にする。
		/// </summary>
		public void Hide()
		{
			if (m_BubbleRoot != null)
			{
				m_BubbleRoot.SetActive(false);
			}
		}

		private Sprite GetSpriteForType(string type)
		{
			switch (type)
			{
				case "move":    return m_MoveSprite;
				case "push":    return m_PushSprite;
				case "attack":  return m_AttackSprite;
				case "defense": return m_DefenseSprite;
				case "skill":   return m_SkillSprite;
				case "rest":    return m_RestSprite;
				default:        return null;
			}
		}
	}
}
