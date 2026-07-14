using System;
using DG.Tweening;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;
using UnityEngine.Serialization;

namespace GamblingAction.Gameplay
{
	public class ItemView : MonoBehaviour
	{
		[FormerlySerializedAs("sprite")]
		[SerializeField] private SpriteRenderer m_Sprite;
		[SerializeField, Tooltip("sprite を保持する子ノード。bob・ピックアップ演出はこの transform に作用する（root の論理位置と分離）")]
		private Transform m_SpriteRoot;
		[FormerlySerializedAs("chipsColor")]
		[SerializeField] private Color m_ChipsColor = new(1f, 0.85f, 0.1f);
		[FormerlySerializedAs("moneyColor")]
		[SerializeField] private Color m_MoneyColor = new(0.2f, 0.9f, 0.4f);
		[FormerlySerializedAs("billboardTarget")]
		[SerializeField] private Transform m_BillboardTarget;
		[FormerlySerializedAs("bobAmplitude")]
		[SerializeField] private float m_BobAmplitude = 0.1f;
		[FormerlySerializedAs("bobSpeed")]
		[SerializeField] private float m_BobSpeed = 2f;

		[Header("Pickup Fx")]
		[SerializeField, Tooltip("ピックアップ演出：開始時の沈み込みの深さ")]
		private float m_PickupDipDepth = 0.15f;
		[SerializeField, Tooltip("ピックアップ演出：沈み込みの長さ（秒）")]
		private float m_PickupDipDuration = 0.1f;
		[SerializeField, Tooltip("ピックアップ演出：上昇のピーク高さ（蓄力後に跳ね上がる距離）")]
		private float m_PickupHopHeight = 0.6f;
		[SerializeField, Tooltip("ピックアップ演出：上昇の長さ（秒）")]
		private float m_PickupHopDuration = 0.25f;
		[SerializeField, Tooltip("ピックアップ演出：上昇中の最大スケール倍率")]
		private float m_PickupHopScale = 1.4f;
		[SerializeField, Tooltip("ピックアップ演出：上昇中の Y 軸回転回数")]
		private float m_PickupHopSpins = 1.5f;
		[SerializeField, Tooltip("ピックアップ演出：消滅フェードの長さ（秒）")]
		private float m_PickupFadeDuration = 0.2f;

		private IBoardCoords m_Board;
		private Camera m_Cam;
		private ItemDto m_Dto;
		private Vector3 m_BasePos;
		private Vector3 m_SpriteRootBaseLocalPos;
		private bool m_BobEnabled = true;
		private bool m_BillboardEnabled = true;
		private Sequence m_PickupSeq;

		public double ItemId => m_Dto?.Id ?? 0;

		public void Bind(ItemDto dto, IBoardCoords board)
		{
			m_Dto = dto;
			m_Board = board;
			m_Cam = Camera.main;

			ApplyColor();
			m_BasePos = m_Board.GridToWorld(dto.X, dto.Y) + Vector3.up * 0.4f;
			transform.position = m_BasePos;
			if (m_SpriteRoot != null)
				m_SpriteRootBaseLocalPos = m_SpriteRoot.localPosition;
		}

		private void ApplyColor()
		{
			if (m_Sprite == null) return;
			m_Sprite.color = m_Dto.Type == "chips" ? m_ChipsColor : m_MoneyColor;
		}

		private void Update()
		{
			if (!m_BobEnabled || m_SpriteRoot == null) return;
			var local = m_SpriteRootBaseLocalPos;
			local.y += Mathf.Sin(Time.time * m_BobSpeed) * m_BobAmplitude;
			m_SpriteRoot.localPosition = local;
		}

		private void LateUpdate()
		{
			if (!m_BillboardEnabled) return;
			if (m_BillboardTarget == null) return;
			if (m_Cam == null) m_Cam = Camera.main;
			if (m_Cam == null) return;
			m_BillboardTarget.rotation = m_Cam.transform.rotation;
		}

		// ピックアップ演出。沈み込み → Y 軸回転＆ホップ → その場でフェード。
		// 演出は SpriteRoot に作用し、root（論理位置）は動かさない。
		public void PlayPickupFx(Action onFinished)
		{
			m_BobEnabled = false;
			// Y 軸回転を生かすため billboard を停止
			m_BillboardEnabled = false;
			if (m_SpriteRoot != null)
				m_SpriteRoot.localPosition = m_SpriteRootBaseLocalPos;
			m_PickupSeq?.Kill();

			var fxTarget = m_SpriteRoot != null ? m_SpriteRoot : transform;

			m_PickupSeq = DOTween.Sequence();
			// 1) 沈み込み
			m_PickupSeq.Append(fxTarget.DOLocalMoveY(m_SpriteRootBaseLocalPos.y - m_PickupDipDepth, m_PickupDipDuration)
				.SetEase(Ease.OutQuad));
			m_PickupSeq.Join(fxTarget.DOScale(0.85f, m_PickupDipDuration).SetEase(Ease.OutQuad));
			// 2) ホップ：拡大しつつ Y 軸回転、頂点まで弾む
			m_PickupSeq.Append(fxTarget.DOLocalMoveY(m_SpriteRootBaseLocalPos.y + m_PickupHopHeight, m_PickupHopDuration)
				.SetEase(Ease.OutQuad));
			m_PickupSeq.Join(fxTarget.DOScale(m_PickupHopScale, m_PickupHopDuration).SetEase(Ease.OutBack));
			m_PickupSeq.Join(fxTarget.DOLocalRotate(
				new Vector3(0f, 360f * m_PickupHopSpins, 0f),
				m_PickupHopDuration,
				RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
			// 3) その場でフェード（縮小して消える）
			m_PickupSeq.Append(fxTarget.DOScale(0.05f, m_PickupFadeDuration).SetEase(Ease.InQuad));
			m_PickupSeq.OnComplete(() => onFinished?.Invoke());
			m_PickupSeq.OnKill(() => onFinished = null);
		}

		private void OnDestroy()
		{
			m_PickupSeq?.Kill();
		}
	}
}
