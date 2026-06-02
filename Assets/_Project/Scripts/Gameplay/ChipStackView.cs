using System.Collections.Generic;
using DG.Tweening;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// 自分の所持チップ数（PlayerDto.Chips）を立体チップの山として表示する。
	/// Chips がそのまま枚数。増えた分は上から落下、減った分は上昇しながら消す。
	/// 3D チップを ChipCamera で RenderTexture に描画し、左下 RawImage に貼る前提。
	/// データは GameStateLocator から自分で取得し、OnPlayersChanged で更新する。
	/// 場面側のセットアップ（カメラ・RT・RawImage）は別途エディタ作業。
	/// </summary>
	public class ChipStackView : MonoBehaviour
	{
		[Header("Chip")]
		[Tooltip("チップ本体の prefab（円柱 mesh + ToonShader material）")]
		[SerializeField] private GameObject m_ChipPrefab;
		[Tooltip("この数を超えたら隣に新しい山を作る")]
		[SerializeField] private int m_MaxPerStack = 20;

		[Header("Layout")]
		[Tooltip("山と山の横間隔")]
		[SerializeField] private float m_StackSpacing = 0.6f;
		[Tooltip("チップ 1 枚分の高さ")]
		[SerializeField] private float m_ChipHeight = 0.08f;
		[Tooltip("チップを生成・配置する親（ChipCamera の写す空間）")]
		[SerializeField] private Transform m_ChipRoot;

		[Header("Animation")]
		[Tooltip("落下の開始高さ（着地点からの相対）")]
		[SerializeField] private float m_DropHeight = 2f;
		[Tooltip("落下開始位置の前後左右の散らばり。大きいほど「一掴みを撒いた」感じになる。")]
		[SerializeField] private float m_SpawnSpread = 0.3f;
		[Tooltip("落下にかける秒数")]
		[SerializeField] private float m_DropDuration = 0.35f;
		[Tooltip("一度に複数枚動くとき、1 枚ごとに開始をずらす秒数。0 で同時。")]
		[SerializeField] private float m_DropStagger = 0.08f;
		[Tooltip("最後の 1 枚が動き出すまでの時間の上限（秒）。枚数が多いとこの中に収まるよう間隔を自動で詰める。")]
		[SerializeField] private float m_MaxStaggerTotal = 1.8f;
		[Tooltip("空の山への初回撒き下ろし専用の総時間（秒）。準備カウントダウン中に収める。")]
		[SerializeField] private float m_FirstFillTotal = 3.5f;
		[Tooltip("消費時に上昇する高さ")]
		[SerializeField] private float m_RiseHeight = 0.8f;
		[Tooltip("消費アニメの秒数")]
		[SerializeField] private float m_ConsumeDuration = 0.3f;
		[Tooltip("着地時の横ずれ量（積み重ねの錯位感）")]
		[SerializeField] private float m_LandingJitter = 0.02f;
		[Tooltip("正面から見たときの傾き（Z 軸回転）の最大角度。±この範囲で乱数化。")]
		[SerializeField] private float m_TiltAngle = 8f;

		// 現在シーンに存在するチップインスタンスを下から順に保持。
		private readonly List<Transform> m_Live = new();

		private IGameState m_State;

		private void Awake()
		{
			if (m_ChipRoot == null) m_ChipRoot = transform;
		}

		// このパネルが表示（SetActive）された時点で動く。その時には
		// GameStateLocator は既に初期化済みの想定。PlayerSpawner と同じ流儀。
		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[ChipStack] GameStateLocator.Current is null. ゲーム開始後に表示される想定。");
				return;
			}

			m_State.OnPlayersChanged += HandlePlayersChanged;
			HandlePlayersChanged(); // 初期表示
		}

		private void OnDestroy()
		{
			if (m_State != null) m_State.OnPlayersChanged -= HandlePlayersChanged;
		}

		private void HandlePlayersChanged()
		{
			Apply(m_State?.Me);
		}

		public void Apply(PlayerDto dto)
		{
			if (dto == null || m_ChipPrefab == null) return;

			int target = dto.Chips;

			int delta = Mathf.Abs(target - m_Live.Count);
			// 空の山への初回撒き下ろしは時間窓を長めに取る（準備カウントダウン中に収める）。
			float cap = m_Live.Count == 0 ? m_FirstFillTotal : m_MaxStaggerTotal;
			// 全体をこの時間窓に収め、各枚はその中のランダムな時刻に動き出す。
			float window = WindowFor(delta, cap);

			while (m_Live.Count < target)
			{
				var chip = SpawnChip(m_Live.Count, Random.Range(0f, window));
				m_Live.Add(chip);
			}

			while (m_Live.Count > target)
			{
				int last = m_Live.Count - 1;
				ConsumeChip(m_Live[last], Random.Range(0f, window));
				m_Live.RemoveAt(last);
			}
		}

		// n 枚を撒くときの時間窓。基本は DropStagger×(n-1)、ただし cap で頭打ち。
		private float WindowFor(int count, float cap)
		{
			if (count <= 1) return 0f;
			return Mathf.Min(m_DropStagger * (count - 1), cap);
		}

		// delay: 落下開始までの待ち時間（秒）。複数枚を 1 枚ずつずらすのに使う。
		private Transform SpawnChip(int index, float delay)
		{
			Vector3 landing = SlotPosition(index);

			var go = Instantiate(m_ChipPrefab, m_ChipRoot);
			var t = go.transform;

			// 着地点を枚ごとに前後左右へずらして積み重ねの錯位感を出す
			Vector3 settle = landing + new Vector3(
				Random.Range(-m_LandingJitter, m_LandingJitter),
				0f,
				Random.Range(-m_LandingJitter, m_LandingJitter));

			// 落下開始位置を前後左右に散らす（落下先は settle で確定済み）。
			float startX = settle.x + Random.Range(-m_SpawnSpread, m_SpawnSpread);
			float startZ = settle.z + Random.Range(-m_SpawnSpread, m_SpawnSpread);
			t.localPosition = new Vector3(startX, settle.y + m_DropHeight, startZ);
			// 落下中は傾けておく。Y はランダムな向き、Z は正面から見た傾き（±m_TiltAngle）。
			float yaw = Random.Range(0f, 360f);
			t.localRotation = Quaternion.Euler(0f, yaw, Random.Range(-m_TiltAngle, m_TiltAngle));

			var seq = DOTween.Sequence();
			seq.SetDelay(delay);
			seq.Append(t.DOLocalMove(settle, m_DropDuration).SetEase(Ease.OutBounce));
			// 着地に合わせて傾きを水平へ戻す（向き yaw は保持）。
			seq.Join(t.DOLocalRotate(new Vector3(0f, yaw, 0f), m_DropDuration).SetEase(Ease.OutQuad));
			return t;
		}

		// delay: 上昇開始までの待ち時間（秒）。複数枚を 1 枚ずつずらすのに使う。
		private void ConsumeChip(Transform t, float delay)
		{
			if (t == null) return;

			Vector3 up = t.localPosition + Vector3.up * m_RiseHeight;
			// 上昇中に傾ける（落下の逆。向き yaw は保持）。
			float yaw = t.localEulerAngles.y;
			Vector3 tilted = new(0f, yaw, Random.Range(-m_TiltAngle, m_TiltAngle));

			var seq = DOTween.Sequence();
			seq.AppendInterval(delay);
			seq.Append(t.DOLocalMove(up, m_ConsumeDuration).SetEase(Ease.InQuad));
			seq.Join(t.DOLocalRotate(tilted, m_ConsumeDuration).SetEase(Ease.OutQuad));
			FadeOut(t, m_ConsumeDuration, seq);
			seq.OnComplete(() => { if (t != null) Destroy(t.gameObject); });
		}

		// マテリアル alpha を DOTween.To でフェード（DOFade 系は Pro 限定のため使わない）
		private static void FadeOut(Transform t, float duration, Sequence seq)
		{
			var renderer = t.GetComponentInChildren<Renderer>();
			if (renderer == null) return;

			var mat = renderer.material;
			if (!mat.HasProperty("_BaseColor")) return;

			seq.Join(DOTween.To(
				() => mat.GetColor("_BaseColor").a,
				a =>
				{
					Color c = mat.GetColor("_BaseColor");
					c.a = a;
					mat.SetColor("_BaseColor", c);
				},
				0f, duration));
		}

		// index 番目のチップの着地位置。MaxPerStack を超えたら横に山を分ける。
		private Vector3 SlotPosition(int index)
		{
			int perStack = Mathf.Max(1, m_MaxPerStack);
			int stackIndex = index / perStack;
			int heightIndex = index % perStack;

			return new Vector3(
				stackIndex * m_StackSpacing,
				heightIndex * m_ChipHeight,
				0f);
		}
	}
}
