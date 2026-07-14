using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI.Editor
{
	// DisconnectNoticeView 用の全画面パネルを生成するメニュー。
	// GameInstaller の prefab を開き、ルートを選択した状態で実行する。
	// 再実行すると既存のパネルを消して作り直す。
	public static class DisconnectNoticePanelGenerator
	{
		private const string k_CanvasName = "DisconnectNoticeCanvas";
		private const string k_FontPath = "Assets/_Project/Font/太字/DelaGothicOne-Regular SDF.asset";
		private const string k_Message = "相手が落ちました。\nゲームを終了して再起動してください。";
		private const string k_ButtonLabel = "ゲームを終了";

		[MenuItem("GamblingAction/UI/相手切断パネルを生成")]
		private static void Generate()
		{
			var root = Selection.activeGameObject;
			if (root == null)
			{
				EditorUtility.DisplayDialog("相手切断パネル",
					"GameInstaller の prefab を開き、ルートを選択してから実行してください。", "OK");
				return;
			}

			// 既存の生成物を消してから作り直す。
			var oldCanvas = root.transform.Find(k_CanvasName);
			if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas.gameObject);
			var oldView = root.GetComponent<DisconnectNoticeView>();
			if (oldView != null) Undo.DestroyObjectImmediate(oldView);

			var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);
			if (font == null)
				Debug.LogWarning($"[DisconnectNotice] フォントが見つかりません: {k_FontPath}（後で手動設定してください）");

			// Canvas を無効にすると View が動かなくなるため、表示/非表示は Scrim 以下で切り替える。
			var canvasGo = new GameObject(k_CanvasName);
			Undo.RegisterCreatedObjectUndo(canvasGo, "Create " + k_CanvasName);
			canvasGo.transform.SetParent(root.transform, false);
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			// 他の Canvas より確実に手前へ出す。
			canvas.sortingOrder = 32000;
			var scaler = canvasGo.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			canvasGo.AddComponent<GraphicRaycaster>();

			// 暗幕。Raycast を受けるので背後の操作もここで遮られる。
			var scrim = CreateChild(canvasGo.transform, "Scrim");
			Stretch(scrim);
			var scrimImage = scrim.gameObject.AddComponent<Image>();
			scrimImage.color = new Color(0f, 0f, 0f, 0.85f);

			// メッセージ
			var message = CreateChild(scrim, "Message");
			message.anchorMin = message.anchorMax = new Vector2(0.5f, 0.5f);
			message.anchoredPosition = new Vector2(0f, 80f);
			message.sizeDelta = new Vector2(1400f, 300f);
			var messageText = message.gameObject.AddComponent<TextMeshProUGUI>();
			if (font != null) messageText.font = font;
			messageText.text = k_Message;
			messageText.fontSize = 56f;
			messageText.color = Color.white;
			messageText.alignment = TextAlignmentOptions.Center;

			// 終了ボタン
			var button = CreateChild(scrim, "QuitButton");
			button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.5f);
			button.anchoredPosition = new Vector2(0f, -140f);
			button.sizeDelta = new Vector2(420f, 100f);
			var buttonImage = button.gameObject.AddComponent<Image>();
			buttonImage.color = new Color(0.9f, 0.18f, 0.18f);
			var buttonComponent = button.gameObject.AddComponent<Button>();
			buttonComponent.targetGraphic = buttonImage;

			var buttonLabel = CreateChild(button, "Text");
			Stretch(buttonLabel);
			var buttonText = buttonLabel.gameObject.AddComponent<TextMeshProUGUI>();
			if (font != null) buttonText.font = font;
			buttonText.text = k_ButtonLabel;
			buttonText.fontSize = 40f;
			buttonText.color = Color.white;
			buttonText.alignment = TextAlignmentOptions.Center;

			var view = canvasGo.AddComponent<DisconnectNoticeView>();
			var so = new SerializedObject(view);
			so.FindProperty("m_Panel").objectReferenceValue = scrim.gameObject;
			so.FindProperty("m_QuitButton").objectReferenceValue = buttonComponent;
			so.ApplyModifiedProperties();

			EditorUtility.SetDirty(root);
			Selection.activeGameObject = canvasGo;
			Debug.Log("[DisconnectNotice] パネルを生成しました。prefab を保存してください。");
		}

		private static RectTransform CreateChild(Transform parent, string name)
		{
			var go = new GameObject(name, typeof(RectTransform));
			Undo.RegisterCreatedObjectUndo(go, "Create " + name);
			var rect = go.GetComponent<RectTransform>();
			rect.SetParent(parent, false);
			return rect;
		}

		private static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}
	}
}
