using UnityEditor;
using UnityEngine;

namespace GamblingAction.Gameplay.Editor
{
	// PlayerFxController の Inspector を効果ごとに折りたたみ可能なグループに分けて表示する。
	// 効果が増えても各グループを畳んで目的の項目を探しやすくするためのもの。
	[CustomEditor(typeof(PlayerFxController))]
	[CanEditMultipleObjects]
	public class PlayerFxControllerEditor : UnityEditor.Editor
	{
		// 折りたたみ状態は SessionState に保存し、ドメインリロードをまたいで維持する。
		private const string k_KeyPrefix = "PlayerFxControllerEditor.Foldout.";

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawField("m_SpriteRoot");
			EditorGUILayout.Space();

			DrawGroup("登場ディゾルブ",
				"m_DissolveSprite", "m_AppearDuration", "m_AppearEase");

			DrawGroup("被弾シェイク（ランダム方向）",
				"m_HitShakeStrength", "m_HitShakeDuration", "m_HitShakeVibrato");

			DrawGroup("押し出されパンチ（押された方向）",
				"m_PushedPunchStrength", "m_PushedPunchDuration", "m_PushedPunchVibrato");

			DrawGroup("移動衝突パンチ（衝突した方向）",
				"m_BumpPunchStrength", "m_BumpPunchDuration", "m_BumpPunchVibrato");

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawGroup(string title, params string[] propertyNames)
		{
			string key = k_KeyPrefix + title;
			bool expanded = SessionState.GetBool(key, true);

			expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
			SessionState.SetBool(key, expanded);

			if (expanded)
			{
				EditorGUI.indentLevel++;
				foreach (var name in propertyNames)
					DrawField(name);
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.EndFoldoutHeaderGroup();
			EditorGUILayout.Space();
		}

		private void DrawField(string propertyName)
		{
			var prop = serializedObject.FindProperty(propertyName);
			if (prop != null) EditorGUILayout.PropertyField(prop);
		}
	}
}
