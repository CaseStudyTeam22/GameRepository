using GamblingAction.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	// 相手が完全に切断した（サーバの猶予時間を過ぎて player_left が届いた）ときに、
	// 全画面のお知らせを出して終了を促す。
	// 現状のサーバは片方が抜けた状態から復帰できないため、続行させずに再起動へ誘導する。
	//
	// GameInstaller と同じオブジェクトに付けて常駐させる。どのシーンでも同じ表示を出すため、
	// シーン側に配置物を持たず、UI はこのスクリプトが実行時に組み立てる。
	public class DisconnectNoticeView : MonoBehaviour
	{
		private const string k_Message = "相手が落ちました。\nゲームを終了して再起動してください。";
		private const string k_ButtonLabel = "ゲームを終了";

		// 他の Canvas より確実に手前へ出す。
		private const int k_SortingOrder = 32000;

		private IGameState m_State;
		private GameObject m_Root;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[DisconnectNotice] GameStateLocator.Current is null");
				return;
			}
			m_State.OnPlayerLeft += HandlePlayerLeft;
		}

		private void OnDestroy()
		{
			if (m_State == null) return;
			m_State.OnPlayerLeft -= HandlePlayerLeft;
		}

		private void HandlePlayerLeft(string id)
		{
			// 自分が抜けた通知（自分の切断が確定した場合）は対象外。
			if (id == m_State.MyId) return;
			// 既に出ていれば作り直さない。
			if (m_Root != null) return;

			Build();
		}

		private static void Quit()
		{
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		// 全画面の暗幕 + メッセージ + 終了ボタンを組み立てる。
		// 暗幕が Raycast を受けるので、背後の操作はこの時点で遮られる。
		private void Build()
		{
			m_Root = new GameObject("DisconnectNoticeCanvas");
			m_Root.transform.SetParent(transform, false);

			var canvas = m_Root.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = k_SortingOrder;

			var scaler = m_Root.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);

			m_Root.AddComponent<GraphicRaycaster>();

			var scrim = CreateChild(m_Root.transform, "Scrim");
			Stretch(scrim);
			var scrimImage = scrim.gameObject.AddComponent<Image>();
			scrimImage.color = new Color(0f, 0f, 0f, 0.85f);

			var label = CreateChild(scrim, "Message");
			label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
			label.pivot = new Vector2(0.5f, 0.5f);
			label.anchoredPosition = new Vector2(0f, 80f);
			label.sizeDelta = new Vector2(1400f, 300f);
			var text = label.gameObject.AddComponent<TextMeshProUGUI>();
			text.text = k_Message;
			text.fontSize = 56f;
			text.color = Color.white;
			text.alignment = TextAlignmentOptions.Center;

			var button = CreateChild(scrim, "QuitButton");
			button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.5f);
			button.pivot = new Vector2(0.5f, 0.5f);
			button.anchoredPosition = new Vector2(0f, -140f);
			button.sizeDelta = new Vector2(420f, 100f);
			var buttonImage = button.gameObject.AddComponent<Image>();
			buttonImage.color = new Color(0.9f, 0.18f, 0.18f);
			var buttonComponent = button.gameObject.AddComponent<Button>();
			buttonComponent.targetGraphic = buttonImage;
			buttonComponent.onClick.AddListener(Quit);

			var buttonLabel = CreateChild(button, "Text");
			Stretch(buttonLabel);
			var buttonText = buttonLabel.gameObject.AddComponent<TextMeshProUGUI>();
			buttonText.text = k_ButtonLabel;
			buttonText.fontSize = 40f;
			buttonText.color = Color.white;
			buttonText.alignment = TextAlignmentOptions.Center;
		}

		private static RectTransform CreateChild(Transform parent, string name)
		{
			var go = new GameObject(name, typeof(RectTransform));
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
