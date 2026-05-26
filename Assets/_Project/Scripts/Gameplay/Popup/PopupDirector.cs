using System;
using System.Collections;
using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay.PopupFx
{
	public class PopupDirector : MonoBehaviour
	{
		public static PopupDirector Instance { get; private set; }

		// ── popup の意味的カテゴリ。prefab はカテゴリ単位で差し替え可能 ──
		public enum EPopupKind
		{
			ChipsGain,
			ChipsLoss,
			HitDamage,
			PushedAttack,    // push 招式が成立して相手を押し出した時
			BumpPushed,      // 移動衝突で押し負けた時（"PUSHED!"）
			BumpBlocked,     // 移動衝突で弾かれた時（"BLOCKED"）
			BumpKicked       // head-on 対冲で蹴り出された時（"KICKED!"）
		}

		[Serializable]
		public class PopupBinding
		{
			public EPopupKind Kind;
			[Tooltip("IPopupView を実装した prefab。空なら m_DefaultPrefab を使用")]
			public MonoBehaviour PrefabBehaviour; // IPopupView を実装する MonoBehaviour（Inspector 表示用）
			[Tooltip("デフォルト prefab に渡すヒント色（パーティクル版は無視可）")]
			public Color HintColor = Color.white;
		}

		[Header("Default Prefab")]
		[Tooltip("カテゴリ別 prefab が未指定の時の fallback。IPopupView を実装")]
		[SerializeField] private MonoBehaviour m_DefaultPrefab;

		[Header("Bindings")]
		[Tooltip("カテゴリごとの prefab と色設定")]
		[SerializeField] private List<PopupBinding> m_Bindings = new();

		[Header("Spawn Offset")]
		[Tooltip("プレイヤー頭上のオフセット（ワールド単位）")]
		[SerializeField] private Vector3 m_HeadOffset = new(0f, 1.6f, 0f);

		[Header("Event Subscription")]
		[Tooltip("IGameState のイベント（Hit/Pushed/Vfx-bump）を購読する。Sandbox では false でよい")]
		[SerializeField] private bool m_SubscribeGameEvents = true;

		[Header("Anti-Overlap")]
		[Tooltip("この時間以内に同じプレイヤーで再生された popup は『同時並発』とみなし、高さをずらす")]
		[SerializeField] private float m_ConcurrencyWindow = 0.05f;
		[Tooltip("高さずらしの一段あたりの距離")]
		[SerializeField] private float m_StackOffsetY = 0.45f;
		[Tooltip("時間ずらしの遅延量（並発でない場合の自然な間隔確保用）")]
		[SerializeField] private float m_TimeStaggerDelay = 0.15f;
		[Tooltip("この時間以内に再発火された popup は時間ずらしを適用")]
		[SerializeField] private float m_TimeStaggerWindow = 0.15f;

		private IGameState m_State;
		private readonly Dictionary<string, Transform> m_PlayerAnchors = new();
		private readonly Dictionary<string, PlayerStackInfo> m_StackInfo = new();
		private readonly Dictionary<string, int> m_PrevChips = new();
		private bool m_ChipsSnapshotInitialized;

		// 重複ガード：直近にスポーンした (playerId+text) を記録して同フレーム内の重複発火を抑える
		private readonly Dictionary<string, float> m_RecentSpawns = new();
		private const float kRecentSpawnWindow = 0.08f;

		// prefab 単位のプール（インスタンス ID をキーに分桶）
		private readonly Dictionary<int, Queue<IPopupView>> m_Pool = new();
		// 返却時に prefab を逆引きするための紐付け
		private readonly Dictionary<IPopupView, int> m_PopupToPrefab = new();

		// カテゴリ → binding のキャッシュ
		private readonly Dictionary<EPopupKind, PopupBinding> m_BindingMap = new();

		private struct PlayerStackInfo
		{
			public float LastSpawnTime;
			public int   ActiveStackCount;
		}

		private bool m_DuplicateInstance;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				m_DuplicateInstance = true;
				Destroy(gameObject);
				return;
			}
			Instance = this;

			BuildBindingMap();
		}

		private void Start()
		{
			if (m_DuplicateInstance) return;
			if (!m_SubscribeGameEvents) return;

			m_State = GameStateLocator.Current;
			if (m_State != null)
			{
				m_State.OnGameEvents       += HandleEvents;
				m_State.OnPlayersChanged   += HandlePlayersChanged;
				m_State.OnStateInitialized += SnapshotChips;
				SnapshotChips();
			}
			else
			{
				Debug.LogWarning("[PopupDirector] GameStateLocator.Current が null。イベント駆動 popup は発火しません（Sandbox なら m_SubscribeGameEvents を OFF に）");
			}
		}

		private void OnDestroy()
		{
			if (m_State != null)
			{
				m_State.OnGameEvents       -= HandleEvents;
				m_State.OnPlayersChanged   -= HandlePlayersChanged;
				m_State.OnStateInitialized -= SnapshotChips;
			}
			if (Instance == this) Instance = null;
		}

		// ── chips 差分監視（初期化と更新時の比較用）─────────────
		private void SnapshotChips()
		{
			if (m_State == null) return;
			m_PrevChips.Clear();
			foreach (var kv in m_State.Players)
				m_PrevChips[kv.Key] = kv.Value.Chips;
			m_ChipsSnapshotInitialized = true;
		}

		private void HandlePlayersChanged()
		{
			if (m_State == null) return;
			if (!m_ChipsSnapshotInitialized) { SnapshotChips(); return; }

			var myId = m_State.MyId;
			foreach (var kv in m_State.Players)
			{
				var id = kv.Key;
				var current = kv.Value.Chips;
				if (m_PrevChips.TryGetValue(id, out var prev))
				{
					var delta = current - prev;
					// chips は伏せ情報なので、自分の変動のみ表示する
					if (delta != 0 && id == myId) SpawnChipsDelta(id, delta);
				}
				m_PrevChips[id] = current;
			}
		}

		private void BuildBindingMap()
		{
			m_BindingMap.Clear();
			foreach (var b in m_Bindings)
			{
				if (b == null) continue;
				m_BindingMap[b.Kind] = b;
			}
		}

		// JS 版に準拠したデフォルト色。Bindings に該当 kind がない場合の fallback
		private static Color DefaultColorOf(EPopupKind kind) => kind switch
		{
			EPopupKind.ChipsGain    => new Color(1f,    0.85f, 0.1f, 1f),  // 金: #ffd91a
			EPopupKind.ChipsLoss    => new Color(0.65f, 0.35f, 1f,   1f),  // 紫
			EPopupKind.HitDamage    => new Color(1f,    0.27f, 0.27f, 1f), // 赤: #ff4444
			EPopupKind.PushedAttack => new Color(1f,    0.8f,  0f,   1f),  // 金: #ffcc00
			EPopupKind.BumpPushed   => new Color(1f,    0.53f, 0f,   1f),  // 橙: #ff8800
			EPopupKind.BumpBlocked  => new Color(1f,    0.53f, 0f,   1f),  // 橙
			EPopupKind.BumpKicked   => new Color(1f,    0.27f, 0.27f, 1f), // 赤
			_                       => Color.white
		};

		// ── プレイヤー登録 ────────────────────────────────────────
		public void RegisterPlayer(string playerId, Transform anchor)
		{
			m_PlayerAnchors[playerId] = anchor;
		}

		public void UnregisterPlayer(string playerId)
		{
			m_PlayerAnchors.Remove(playerId);
			m_StackInfo.Remove(playerId);
		}

		// ── イベント駆動 popup ────────────────────────────────────
		private void HandleEvents(EventDto[] events)
		{
			if (events == null) return;
			foreach (var ev in events)
			{
				switch (ev.Type)
				{
					case EventTypes.Hit:
						if (!string.IsNullOrEmpty(ev.TargetId) && ev.Damage.HasValue)
							SpawnByKind(ev.TargetId, EPopupKind.HitDamage, $"-{ev.Damage.Value}");
						break;

					case EventTypes.Pushed:
						if (!string.IsNullOrEmpty(ev.TargetId))
							SpawnByKind(ev.TargetId, EPopupKind.PushedAttack, "KNOCKBACK!");
						break;

					case EventTypes.Vfx:
						if (ev.VfxType == VfxTypes.Bump && !string.IsNullOrEmpty(ev.TargetId))
						{
							var text = string.IsNullOrEmpty(ev.Text) ? "BLOCKED" : ev.Text;
							var kind = text switch
							{
								"PUSHED!" => EPopupKind.BumpPushed,
								"KICKED!" => EPopupKind.BumpKicked,
								_         => EPopupKind.BumpBlocked
							};
							SpawnByKind(ev.TargetId, kind, text);
						}
						break;
				}
			}
		}

		// ── 数字 popup（ChipsDeltaWatcher から呼ばれる）─────────────
		public void SpawnChipsDelta(string playerId, int delta)
		{
			if (delta == 0) return;
			var kind = delta > 0 ? EPopupKind.ChipsGain : EPopupKind.ChipsLoss;
			var sign = delta > 0 ? "+" : "";
			SpawnByKind(playerId, kind, $"{sign}{delta}");
		}

		// ── カテゴリ指定 spawn（外部からも呼べる）─────────────
		public void SpawnByKind(string playerId, EPopupKind kind, string text)
		{
			var prefab = ResolvePrefab(kind, out var hintColor);
			if (prefab == null)
			{
				Debug.LogWarning($"[PopupDirector] prefab が解決できない（kind={kind}）。m_DefaultPrefab か該当 binding を設定してください");
				return;
			}
			Spawn(playerId, prefab, text, hintColor);
		}

		// ── デバッグ用任意 popup（生 prefab 指定）─────────────
		public void SpawnRaw(string playerId, string text, Color color)
		{
			if (m_DefaultPrefab == null)
			{
				Debug.LogWarning("[PopupDirector] m_DefaultPrefab 未設定");
				return;
			}
			Spawn(playerId, m_DefaultPrefab, text, color);
		}

		private MonoBehaviour ResolvePrefab(EPopupKind kind, out Color hintColor)
		{
			if (m_BindingMap.TryGetValue(kind, out var b))
			{
				hintColor = b.HintColor;
				return b.PrefabBehaviour != null ? b.PrefabBehaviour : m_DefaultPrefab;
			}
			hintColor = DefaultColorOf(kind);
			return m_DefaultPrefab;
		}

		// ── 内部実装 ────────────────────────────────────────
		private void Spawn(string playerId, MonoBehaviour prefab, string text, Color color)
		{
			if (!m_PlayerAnchors.TryGetValue(playerId, out var anchor) || anchor == null)
				return;
			if (prefab == null) return;
			if (prefab is not IPopupView)
			{
				Debug.LogError($"[PopupDirector] prefab '{prefab.name}' が IPopupView を実装していない");
				return;
			}

			float now = Time.time;

			// 重複ガード：直近窓内に同じ (player + text) が来たら抑制
			var dedupKey = $"{playerId}|{text}";
			if (m_RecentSpawns.TryGetValue(dedupKey, out var lastT) && now - lastT < kRecentSpawnWindow)
				return;
			m_RecentSpawns[dedupKey] = now;
			m_StackInfo.TryGetValue(playerId, out var info);

			// ── 並発（同時刻）→ 高さずらし、近接（短時間内）→ 時間ずらし ──
			float dt = now - info.LastSpawnTime;
			float heightOffset = 0f;
			float delay = 0f;

			if (dt < m_ConcurrencyWindow)
			{
				info.ActiveStackCount++;
				heightOffset = info.ActiveStackCount * m_StackOffsetY;
			}
			else if (dt < m_TimeStaggerWindow)
			{
				delay = m_TimeStaggerDelay;
				info.ActiveStackCount = 0;
			}
			else
			{
				info.ActiveStackCount = 0;
			}

			info.LastSpawnTime = now + delay;
			m_StackInfo[playerId] = info;

			// アンカーオフセット = ヘッドオフセット + 並発時の高さずらし
			Vector3 anchorOffset = m_HeadOffset + new Vector3(0f, heightOffset, 0f);

			if (delay > 0f)
				StartCoroutine(SpawnDelayed(prefab, text, color, anchor, anchorOffset, delay));
			else
				PlayPopup(prefab, text, color, anchor, anchorOffset);
		}

		private IEnumerator SpawnDelayed(MonoBehaviour prefab, string text, Color color, Transform anchor, Vector3 anchorOffset, float delay)
		{
			yield return new WaitForSeconds(delay);
			if (anchor == null) yield break;
			// 時間ずらしの場合 heightOffset は維持しない
			PlayPopup(prefab, text, color, anchor, m_HeadOffset);
		}

		private void PlayPopup(MonoBehaviour prefab, string text, Color color, Transform anchor, Vector3 anchorOffset)
		{
			var view = Acquire(prefab);
			if (view == null) return;
			view.GameObject.transform.SetParent(transform, true);
			view.GameObject.SetActive(true);
			view.Play(text, color, anchor, anchorOffset, OnPopupFinished);
		}

		private IPopupView Acquire(MonoBehaviour prefab)
		{
			int prefabKey = prefab.GetInstanceID();
			if (m_Pool.TryGetValue(prefabKey, out var queue))
			{
				while (queue.Count > 0)
				{
					var p = queue.Dequeue();
					if (p != null && p.GameObject != null) return p;
				}
			}

			var instance = Instantiate(prefab);
			if (instance is not IPopupView view)
			{
				Destroy(instance.gameObject);
				return null;
			}
			m_PopupToPrefab[view] = prefabKey;
			return view;
		}

		private void OnPopupFinished(IPopupView popup)
		{
			if (popup == null || popup.GameObject == null) return;
			popup.GameObject.SetActive(false);
			if (m_PopupToPrefab.TryGetValue(popup, out var key))
			{
				if (!m_Pool.TryGetValue(key, out var queue))
				{
					queue = new Queue<IPopupView>();
					m_Pool[key] = queue;
				}
				queue.Enqueue(popup);
			}
			else
			{
				// 未登録 popup は破棄
				UnityEngine.Object.Destroy(popup.GameObject);
			}
		}
	}
}
