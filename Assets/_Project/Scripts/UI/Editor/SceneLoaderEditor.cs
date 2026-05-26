using UnityEditor;

namespace GamblingAction.UI.Editor
{
	// SceneLoader の Inspector。Scene Name は Load On Start を使うときだけ表示する。
	// ボタンから LoadScene(string) を呼ぶ場合は Scene Name を参照しないため、
	// 不要な項目を隠して混乱を防ぐ。
	[CustomEditor(typeof(SceneLoader))]
	public class SceneLoaderEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var loadOnStart = serializedObject.FindProperty("m_LoadOnStart");
			EditorGUILayout.PropertyField(loadOnStart);

			// Load On Start が有効なときのみ遷移先を入力させる。
			if (loadOnStart.boolValue)
				EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SceneName"));

			serializedObject.ApplyModifiedProperties();
		}
	}
}
