using DG.Tweening;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// ファイナルレイズ本番中だけ SpaceDome の色相をずらす。
	/// 本番開始（OnFinalRaiseStarted）で目標値へトゥイーンし、
	/// 中断・終了（OnFinalRaiseCanceled / OnGameOver）で元へ戻す。
	///
	/// 対象は GamblingAction/SpaceDome マテリアルの _HueShift（0～1、0 で無変化）。
	/// </summary>
	public class SpaceDomeHueController : MonoBehaviour
	{
		[SerializeField, Tooltip("SpaceDome の Renderer。マテリアルは GamblingAction/SpaceDome を想定")]
		private Renderer m_DomeRenderer;

		[SerializeField, Tooltip("ファイナルレイズ本番中の色相ずらし量（0～1）")]
		private float m_FinalRaiseHueShift = 0.8f;

		[SerializeField, Tooltip("色相が切り替わるトゥイーンの長さ（秒）")]
		private float m_TweenDuration = 0.8f;

		[SerializeField, Tooltip("色相トゥイーンのイージング")]
		private Ease m_TweenEase = Ease.InOutSine;

		private static readonly int s_HueShift = Shader.PropertyToID("_HueShift");

		private IGameState m_State;
		private Material m_Material;
		private float m_BaseHueShift;
		private Tween m_HueTween;

		private void Awake()
		{
			if (m_DomeRenderer != null)
			{
				// シーン上の球 1 個ぶんなのでインスタンス化したマテリアルを直接書き換える。
				m_Material = m_DomeRenderer.material;

                if (m_Material == null)
                {
                    Debug.LogError("Material is null");
                    return;
                }
                
				m_BaseHueShift = m_Material.GetFloat(s_HueShift);
			}
		}

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[SpaceDomeHue] GameStateLocator.Current is null");
				return;
			}

			m_State.OnFinalRaiseStarted  += HandleStarted;
			m_State.OnFinalRaiseCanceled += HandleCanceled;
			m_State.OnGameOver           += HandleGameOver;
		}

		private void OnDestroy()
		{
			m_HueTween?.Kill();
			if (m_State == null) return;
			m_State.OnFinalRaiseStarted  -= HandleStarted;
			m_State.OnFinalRaiseCanceled -= HandleCanceled;
			m_State.OnGameOver           -= HandleGameOver;
		}

		private void HandleStarted()
		{
			TweenHueTo(m_FinalRaiseHueShift);
		}

		private void HandleCanceled(Core.Dto.FinalRaiseCanceledMessage msg)
		{
			TweenHueTo(m_BaseHueShift);
		}

		private void HandleGameOver(string winnerRole)
		{
			TweenHueTo(m_BaseHueShift);
		}

		private void TweenHueTo(float target)
		{
			if (m_Material == null) return;
			m_HueTween?.Kill();
			float from = m_Material.GetFloat(s_HueShift);
			m_HueTween = DOTween.To(() => from, x => { from = x; m_Material.SetFloat(s_HueShift, x); }, target, m_TweenDuration)
				.SetEase(m_TweenEase);
		}
	}
}
