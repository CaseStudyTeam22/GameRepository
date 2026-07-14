using DG.Tweening;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// プレイヤー sprite に対する視覚効果を一括管理する。
	/// 登場ディゾルブ、被弾 / 押し出し / 衝突時のシェイク・パンチを持つ。
	/// 将来的にカラー変化・マテリアル差し替え等もここに集約する想定。
	///
	/// PlayerView は server イベントを受け取って本コンポーネントの Play 系メソッドを呼ぶ。
	/// 本コンポーネント自体はイベント購読を持たない（純粋な「指示されたら再生する」役割）。
	/// </summary>
	public class PlayerFxController : MonoBehaviour
	{
		[SerializeField, Tooltip("シェイク等の演出を作用させる sprite 子ノード（root の移動 tween と分離するため別 transform）")]
		private Transform m_SpriteRoot;

		[SerializeField, Tooltip("ディゾルブを適用する SpriteRenderer。マテリアルは GamblingAction/SpriteDissolve を想定")]
		private SpriteRenderer m_DissolveSprite;
		[SerializeField, Tooltip("登場ディゾルブの長さ（秒）")]
		private float m_AppearDuration = 0.6f;
		[SerializeField, Tooltip("登場ディゾルブのイージング")]
		private Ease m_AppearEase = Ease.OutQuad;

		[SerializeField, Tooltip("被弾時シェイクの強さ（ワールド単位の最大ずれ）")]
		private float m_HitShakeStrength = 0.05f;
		[SerializeField, Tooltip("被弾時シェイクの長さ（秒）")]
		private float m_HitShakeDuration = 0.3f;
		[SerializeField, Tooltip("被弾時シェイクの振動回数")]
		private int m_HitShakeVibrato = 18;

		[SerializeField, Tooltip("被push時に押された方向へ突き出す距離")]
		private float m_PushedPunchStrength = 0.05f;
		[SerializeField, Tooltip("被push時 punch の長さ（秒）")]
		private float m_PushedPunchDuration = 0.3f;
		[SerializeField, Tooltip("被push時 punch の振動回数（戻り方向の小さな揺れ）")]
		private int m_PushedPunchVibrato = 6;

		[SerializeField, Tooltip("bump時に衝突方向へ突き出す距離")]
		private float m_BumpPunchStrength = 0.05f;
		[SerializeField, Tooltip("bump時 punch の長さ（秒）")]
		private float m_BumpPunchDuration = 0.25f;
		[SerializeField, Tooltip("bump時 punch の振動回数")]
		private int m_BumpPunchVibrato = 8;

		
        [SerializeField, Tooltip("キャラ別スキルエフェクトの prefab。配列の添字 = charaIndex（0=Normal〜6=Debtor）。空欄の枠はエフェクト無し")]
        private GameObject[] m_SkillEffects = new GameObject[7];

        [SerializeField, Tooltip("スキルエフェクトを出す高さオフセット（足元が0。少し浮かせたい場合に使う）")]
        private float m_SkillEffectYOffset = 0f;

        [SerializeField, Tooltip("生成したエフェクトを自動で破棄するまでの秒数。ParticleSystem の再生が終わる長さより少し長めに")]
        private float m_SkillEffectLifetime = 3f;

		private Tween m_SpriteFxTween;
		private Vector3 m_SpriteRootBaseLocalPos;
		private bool m_Initialized;

		private static readonly int s_DissolveAmount = Shader.PropertyToID("_DissolveAmount");
		private static readonly int s_EdgeColor = Shader.PropertyToID("_EdgeColor");
		private MaterialPropertyBlock m_DissolveBlock;
		private Tween m_DissolveTween;

		private void Awake()
		{
			if (m_SpriteRoot != null)
			{
				m_SpriteRootBaseLocalPos = m_SpriteRoot.localPosition;
				m_Initialized = true;
			}
		}

		private void OnDestroy()
		{
			m_SpriteFxTween?.Kill();
			m_DissolveTween?.Kill();
		}

		// 登場時。完全溶解（非表示）から完全表示へアニメーションする。
		public void PlayAppear()
		{
			if (m_DissolveSprite == null) return;
			m_DissolveTween?.Kill();
			SetDissolve(0f);
			float v = 0f;
			m_DissolveTween = DOTween.To(() => v, x => { v = x; SetDissolve(v); }, 1f, m_AppearDuration)
				.SetEase(m_AppearEase);
		}

		// ディゾルブ境界の発光色を設定する。チーム色（赤/青）に合わせるため PlayerView から呼ぶ。
		public void SetEdgeColor(Color color)
		{
			if (m_DissolveSprite == null) return;
			m_DissolveBlock ??= new MaterialPropertyBlock();
			m_DissolveSprite.GetPropertyBlock(m_DissolveBlock);
			m_DissolveBlock.SetColor(s_EdgeColor, color);
			m_DissolveSprite.SetPropertyBlock(m_DissolveBlock);
		}

		// SpriteRenderer に MaterialPropertyBlock で _DissolveAmount を書き込む（インスタンスごとに独立）。
		private void SetDissolve(float amount)
		{
			if (m_DissolveSprite == null) return;
			m_DissolveBlock ??= new MaterialPropertyBlock();
			m_DissolveSprite.GetPropertyBlock(m_DissolveBlock);
			m_DissolveBlock.SetFloat(s_DissolveAmount, amount);
			m_DissolveSprite.SetPropertyBlock(m_DissolveBlock);
		}

		// 被弾時。sprite 子ノードを X/Y 平面でランダム方向に揺らす
		public void PlayHitShake()
		{
			if (!m_Initialized) return;
			ResetSpriteFx();
			m_SpriteFxTween = m_SpriteRoot
				.DOShakePosition(m_HitShakeDuration, m_HitShakeStrength, m_HitShakeVibrato, 90f, false, true);
		}

		// 押し出された方向への punch。dir は EventDto.Dir（"up"/"down"/"left"/"right"）
		public void PlayPushedPunch(string dir)
		{
			PlayDirectionalPunch(dir, m_PushedPunchStrength, m_PushedPunchDuration, m_PushedPunchVibrato);
		}

		// 移動衝突方向への punch。dir は EventDto.Dir
		public void PlayBumpPunch(string dir)
		{
			PlayDirectionalPunch(dir, m_BumpPunchStrength, m_BumpPunchDuration, m_BumpPunchVibrato);
		}

		private void PlayDirectionalPunch(string dir, float strength, float duration, int vibrato)
		{
			if (!m_Initialized) return;
			var worldDir = DirToWorldOffset(dir);
			if (worldDir == Vector3.zero) return;
			// SpriteRoot の親（= Player root）は billboard で毎フレーム カメラ方向に回転される。
			// localPosition の各軸は親の rotation を継承するため、世界方向で指定したい push を
			// 親空間の local 方向に変換してから渡す必要がある。
			var parent = m_SpriteRoot.parent;
			var localOffset = parent != null
				? parent.InverseTransformDirection(worldDir) * strength
				: worldDir * strength;
			ResetSpriteFx();
			// elasticity=1 だと戻りで反対側へ大きく行きすぎるので 0.5 に抑える
			m_SpriteFxTween = m_SpriteRoot
				.DOPunchPosition(localOffset, duration, vibrato, 0.5f, false);
		}

		// 進行中の sprite tween を強制 Complete してから原点へ戻す。
		// Kill だと途中位置で止まり、新 tween の起点がずれて累積偏移する。
		private void ResetSpriteFx()
		{
			if (m_SpriteFxTween != null && m_SpriteFxTween.IsActive())
				m_SpriteFxTween.Complete();
			m_SpriteRoot.localPosition = m_SpriteRootBaseLocalPos;
		}

		// スキル発動時。charaIndex に対応した prefab を足元に生成して再生する。
        // prefab が未設定（空欄）のキャラは何も出さない。
        public void PlaySkill(int charaIndex)
        {
	     Debug.Log($"[PlaySkill] 呼ばれた charaIndex={charaIndex}\n{System.Environment.StackTrace}");
	      // 配列の範囲外や未設定は安全に無視する
	     if (m_SkillEffects == null) return;
	     if (charaIndex < 0 || charaIndex >= m_SkillEffects.Length) return;

	     var prefab = m_SkillEffects[charaIndex];
	     if (prefab == null) return;
	     Debug.Log($"[PlaySkill] prefab生成する {prefab.name}");

	     // 発動プレイヤーの足元位置を求める（この controller は Player 配下に付く想定）
	     var pos = transform.position;
	     pos.y += m_SkillEffectYOffset;

	     // prefab を生成。Hovl のエフェクトは子に複数 ParticleSystem を持つため、
	     // ルートごと生成して丸ごと再生 → 一定時間後に丸ごと破棄する。
	     var instance = Instantiate(prefab, pos, Quaternion.identity);

	     // 生成直後は勝手に再生されるが、念のため全 ParticleSystem を Play しておく
	     var systems = instance.GetComponentsInChildren<ParticleSystem>();
	     Debug.Log($"[PlaySkill] ParticleSystem数={systems.Length}");
	     for (int i = 0; i < systems.Length; i++)
	     {
		  systems[i].Play();
	     }

	      // 再生し終わったら破棄（出しっぱなしでメモリに残さないため）
	      Destroy(instance, m_SkillEffectLifetime);
        }

		// EventDto.Dir を世界空間方向に変換。
		// BoardView.GridToWorld の規約に合わせる：up=+Z（北）/ down=-Z / right=+X / left=-X
		private static Vector3 DirToWorldOffset(string dir)
		{
			return dir switch
			{
				"up"    => new Vector3( 0f, 0f,  1f),
				"down"  => new Vector3( 0f, 0f, -1f),
				"right" => new Vector3( 1f, 0f,  0f),
				"left"  => new Vector3(-1f, 0f,  0f),
				_       => Vector3.zero
			};
		}
	}
}
