using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamblingAction.UI
{
	/// <summary>
	/// マテリアルデザイン風のリップル（波紋）エフェクトを描画するコンポーネント。
	///
	/// 【挙動】
	///   ・クリック／決定された瞬間に、起点から円が広がりながらフェードアウトする。
	///   ・マウスクリック         → クリックした座標を起点にする（IPointerClickHandler）。
	///   ・コントローラー決定（Y） → ボタン中心を起点にする（ISubmitHandler）。
	///
	/// 【設計方針】
	///   ・リップル用の Image はプール（m_Pool）で使い回し、毎クリックの生成/破棄による
	///     GC 発生を抑制する。
	///   ・枠外へリップルがはみ出さないよう、アタッチ先に RectMask2D を自動付与してクリップする。
	///   ・色／最大サイズ／拡大・フェード時間／開始透明度を Inspector で調整可能。
	///     重要ボタン（決定・ALL-IN 等）はこれらの値を強めに設定するだけで「強リップル」化できる。
	///
	/// 【既存コンポーネントとの関係】
	///   ・ButtonFocusHighlight（選択／ホバー時の拡大＋アウトライン発光）とは責務が異なるため共存可能。
	///     こちらは「クリック／決定の単発フィードバック」専用。
	///
	/// 【セットアップ】
	///   ・リップルを出したいボタン（Graphic を持つ GameObject）に本コンポーネントをアタッチする。
	///   ・Active Input Handling が Input System Package か Both であること（プロジェクト既定）。
	///
	/// 【DOTween の使い方について】
	///   ・本スクリプトは DOTween のコア API（Transform.DOScale ／ DOTween.To）のみを使用し、
	///     UGUI モジュール側のショートカット（DOSizeDelta／Graphic.DOFade）は使用しない。
	///   ・理由：現状 DOTween は asmdef 化されておらず、asmdef を持つ GamblingAction.UI からは
	///     UGUI モジュールのショートカットを参照できないため。
	///     既存の LobbyController / ActionBannerView も同じ方針（DOScale + DOTween.To）で実装されており、
	///     本スクリプトもそれに揃えることで asmdef 構成を変更せずに動作する。
	///   ・サイズ変更は sizeDelta ではなく localScale（DOScale）で行う。
	///   ・フェードは Graphic.DOFade ではなく DOTween.To で color.a を補間する。
	/// </summary>
	[RequireComponent(typeof(RectTransform))]
	public class ButtonRippleEffect : MonoBehaviour, IPointerClickHandler, ISubmitHandler
	{
		// ─────────────────────────────────────────────────────────────
		// 定数
		// ─────────────────────────────────────────────────────────────

		/// <summary>リップル Image を生成する際の初期プール数</summary>
		private const int m_InitialPoolSize = 4;

		/// <summary>プールの最大保持数（これを超えた分は破棄してメモリ肥大を防ぐ）</summary>
		private const int m_MaxPoolSize = 8;

		// ─────────────────────────────────────────────────────────────
		// シリアライズフィールド（Inspector 調整項目）
		// ─────────────────────────────────────────────────────────────

		[Header("見た目")]
		/// <summary>リップルの色（α は開始透明度 m_StartAlpha で上書きされる）</summary>
		[SerializeField] private Color m_RippleColor = new Color(1f, 1f, 1f, 1f);

		/// <summary>リップルに使用するスプライト。未指定ならリング（輪っか）スプライトを動的生成する</summary>
		[SerializeField] private Sprite m_RippleSprite;

		/// <summary>
		/// 動的生成リングの太さ（直径に対する比、0〜0.5）。
		/// m_RippleSprite を指定した場合は無視される。0.06 で細め、0.10 で太め。
		/// </summary>
		[Range(0.02f, 0.5f)]
		[SerializeField] private float m_GeneratedRingThickness = 0.06f;

		[Header("サイズ・タイミング")]
		/// <summary>
		/// リップル最大直径の倍率。ボタン対角線長 × この値が最終直径になる。
		/// 1.0 でボタンを覆い切る程度。はみ出し表示なら 1.3〜1.8 で画像のように枠外へ広がる。
		/// 重要ボタンはさらに大きめにすると迫力が出る。
		/// </summary>
		[SerializeField] private float m_MaxSizeMultiplier = 1.4f;

		/// <summary>円が最大サイズまで広がるのにかかる時間（秒）</summary>
		[SerializeField] private float m_ExpandDuration = 0.4f;

		/// <summary>フェードアウトにかかる時間（秒）。拡大と並行して進む</summary>
		[SerializeField] private float m_FadeDuration = 0.4f;

		/// <summary>開始時の透明度（0〜1）。重要ボタンは高めにすると濃く出る</summary>
		[Range(0f, 1f)]
		[SerializeField] private float m_StartAlpha = 0.4f;

		[Header("起点")]
		/// <summary>
		/// 起点を常にボタン中心にするか。
		/// true の場合、マウスクリックでもクリック位置ではなく中心から広がる。
		/// </summary>
		[SerializeField] private bool m_AlwaysFromCenter = false;

		[Header("はみ出し")]
		/// <summary>
		/// リングをボタン枠外へはみ出して表示するか。
		/// true  ：クリップせず、ボタンより大きい円が枠外へ広がる（迫力重視）。
		/// false ：RectMask2D でクリップし、ボタン内に収める（きっちり）。
		/// はみ出し時は隣接ボタンや他 UI の上に一瞬重なる点に注意（レイキャストは透過するので操作の邪魔にはならない）。
		/// </summary>
		[SerializeField] private bool m_Overflow = true;

		// ─────────────────────────────────────────────────────────────
		// 内部状態
		// ─────────────────────────────────────────────────────────────

		/// <summary>本体の RectTransform（サイズ計算・座標変換に使用）</summary>
		private RectTransform m_RectTransform;

		/// <summary>リップル Image をぶら下げる親（クリップ用 RectMask2D を持つ）</summary>
		private RectTransform m_Container;

		/// <summary>使い回し用のリップル Image プール</summary>
		private readonly Stack<Image> m_Pool = new Stack<Image>();

		/// <summary>未指定時に動的生成したリングスプライト（破棄管理用に保持）</summary>
		private Sprite m_GeneratedSprite;

		// ─────────────────────────────────────────────────────────────
		// ライフサイクル
		// ─────────────────────────────────────────────────────────────

		private void Awake()
		{
			m_RectTransform = GetComponent<RectTransform>();
			SetupContainer();
			PrewarmPool();
		}

		private void OnDestroy()
		{
			// 動的生成したスプライトは明示的に破棄する
			if (m_GeneratedSprite != null)
			{
				Destroy(m_GeneratedSprite.texture);
				Destroy(m_GeneratedSprite);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// 初期化
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// リップルをクリップするためのコンテナ（RectMask2D 付き）を子として生成する。
		/// ボタン本体に直接 RectMask2D を付けると子の表示まで巻き込むため、専用の子を用意する。
		/// </summary>
		private void SetupContainer()
		{
			var go = new GameObject("RippleContainer", typeof(RectTransform));
			m_Container = go.GetComponent<RectTransform>();
			m_Container.SetParent(m_RectTransform, false);

			// 親いっぱいに広げる
			m_Container.anchorMin = Vector2.zero;
			m_Container.anchorMax = Vector2.one;
			m_Container.offsetMin = Vector2.zero;
			m_Container.offsetMax = Vector2.zero;

			// はみ出しOFFのときだけ枠外をクリップする。
			// ONのときはクリップしないので、リングがボタンより大きく枠外へ広がる。
			if (!m_Overflow)
			{
				go.AddComponent<RectMask2D>();
			}

			// リップルはボタン本体の描画の手前（最前面）に出す
			m_Container.SetAsLastSibling();

			// クリック判定はボタン本体側で行うため、コンテナはレイキャストを透過させる
			var canvasGroup = go.AddComponent<CanvasGroup>();
			canvasGroup.blocksRaycasts = false;
			canvasGroup.interactable = false;
		}

		/// <summary>初期プールを事前生成して、初回クリック時の生成コストを散らす</summary>
		private void PrewarmPool()
		{
			for (int i = 0; i < m_InitialPoolSize; i++)
			{
				Image img = CreateRippleImage();
				img.gameObject.SetActive(false);
				m_Pool.Push(img);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// 入力ハンドラ
		// ─────────────────────────────────────────────────────────────

		/// <summary>マウスクリック時：クリック位置を起点にリップルを出す</summary>
		public void OnPointerClick(PointerEventData eventData)
		{
			Vector2 localPoint;
			if (m_AlwaysFromCenter ||
				!RectTransformUtility.ScreenPointToLocalPointInRectangle(
					m_RectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
			{
				localPoint = Vector2.zero; // 中心
			}

			SpawnRipple(localPoint);
		}

		/// <summary>コントローラー決定（Y ボタン）時：ボタン中心を起点にリップルを出す</summary>
		public void OnSubmit(BaseEventData eventData)
		{
			SpawnRipple(Vector2.zero);
		}

		// ─────────────────────────────────────────────────────────────
		// リップル生成・アニメーション
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// 指定したローカル座標を起点にリップルを1つ再生する。
		/// </summary>
		/// <param name="localCenter">本体 RectTransform ローカル空間での起点座標</param>
		private void SpawnRipple(Vector2 localCenter)
		{
			Image ripple = GetFromPool();

			var rippleRect = ripple.rectTransform;
			rippleRect.anchoredPosition = localCenter;

			// 最終直径＝ボタン対角線長 × 倍率。
			// DOScale で 0→1 に広げるため、基準サイズ（sizeDelta）を最終直径に固定しておく。
			float diameter = CalcMaxDiameter() * m_MaxSizeMultiplier;
			rippleRect.sizeDelta = new Vector2(diameter, diameter);

			// 初期状態（scale 0・指定透明度）
			rippleRect.localScale = Vector3.zero;
			Color startColor = m_RippleColor;
			startColor.a = m_StartAlpha;
			ripple.color = startColor;
			ripple.gameObject.SetActive(true);

			// 進行中の Tween が残っていれば止める（プール再利用時の安全策）
			rippleRect.DOKill();
			DOTween.Kill(ripple);

			// 拡大（localScale）とフェード（color.a）を並行再生し、完了後にプールへ返却する。
			// UGUI モジュール非依存のコア API のみ使用（DOScale ／ DOTween.To）。
			Sequence seq = DOTween.Sequence();

			seq.Join(rippleRect.DOScale(1f, m_ExpandDuration)
				.SetEase(Ease.OutQuad));

			seq.Join(DOTween.To(() => ripple.color.a, a =>
			{
				var c = ripple.color;
				ripple.color = new Color(c.r, c.g, c.b, a);
			}, 0f, m_FadeDuration).SetEase(Ease.OutQuad));

			seq.OnComplete(() => ReturnToPool(ripple));
			seq.SetTarget(ripple);
		}

		/// <summary>ボタンの対角線長（リップルがボタン全体を覆うのに必要な直径）を求める</summary>
		private float CalcMaxDiameter()
		{
			Vector2 size = m_RectTransform.rect.size;
			return Mathf.Sqrt(size.x * size.x + size.y * size.y);
		}

		// ─────────────────────────────────────────────────────────────
		// プール管理
		// ─────────────────────────────────────────────────────────────

		/// <summary>プールからリップル Image を取り出す。空なら新規生成する</summary>
		private Image GetFromPool()
		{
			return m_Pool.Count > 0 ? m_Pool.Pop() : CreateRippleImage();
		}

		/// <summary>使い終わったリップルをプールへ返却する。上限超過分は破棄する</summary>
		private void ReturnToPool(Image ripple)
		{
			if (ripple == null) return;

			ripple.gameObject.SetActive(false);

			if (m_Pool.Count < m_MaxPoolSize)
			{
				m_Pool.Push(ripple);
			}
			else
			{
				Destroy(ripple.gameObject);
			}
		}

		/// <summary>リップル用の Image を1つ生成する（中心アンカー・レイキャスト無効）</summary>
		private Image CreateRippleImage()
		{
			var go = new GameObject("Ripple", typeof(RectTransform), typeof(Image));
			var rect = go.GetComponent<RectTransform>();
			rect.SetParent(m_Container, false);

			// 中心起点で拡大させるため、アンカー・ピボットを中央に揃える
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = Vector2.zero;

			var img = go.GetComponent<Image>();
			img.sprite = m_RippleSprite != null ? m_RippleSprite : GetGeneratedRingSprite();
			img.raycastTarget = false; // クリックを透過させる
			img.maskable = true;       // RectMask2D でクリップさせる

			return img;
		}

		// ─────────────────────────────────────────────────────────────
		// リングスプライトの動的生成（スプライト未指定時のフォールバック）
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Inspector で m_RippleSprite が未指定の場合に使うリング（輪っか）スプライトを生成して返す。
		/// 一度生成したら使い回す。リングの太さは m_GeneratedRingThickness（直径比）で決まる。
		/// </summary>
		private Sprite GetGeneratedRingSprite()
		{
			if (m_GeneratedSprite != null) return m_GeneratedSprite;

			const int size = 128;
			var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};

			float outerRadius = size * 0.5f;
			// リング太さ（ピクセル）。直径 size に対する比から算出する。
			float thickness = size * m_GeneratedRingThickness;
			float innerRadius = Mathf.Max(0f, outerRadius - thickness);
			Vector2 center = new Vector2(outerRadius, outerRadius);
			var pixels = new Color32[size * size];

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

					// 外周・内周ともに 1px ぼかしてジャギーを抑える。
					// 外側：dist <= outerRadius で 1、超えると 0 へ。
					// 内側：dist >= innerRadius で 1、下回ると 0 へ（中央を空洞にする）。
					float outerAlpha = Mathf.Clamp01(outerRadius - dist);
					float innerAlpha = Mathf.Clamp01(dist - innerRadius);
					float alpha = Mathf.Min(outerAlpha, innerAlpha);

					pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
				}
			}

			tex.SetPixels32(pixels);
			tex.Apply();

			m_GeneratedSprite = Sprite.Create(
				tex,
				new Rect(0f, 0f, size, size),
				new Vector2(0.5f, 0.5f),
				100f);

			return m_GeneratedSprite;
		}
	}
}