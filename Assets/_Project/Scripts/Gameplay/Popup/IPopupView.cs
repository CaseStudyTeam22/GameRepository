using System;
using UnityEngine;

namespace GamblingAction.Gameplay.PopupFx
{
	/// <summary>
	/// Popup の最小インターフェース。実装は自身の prefab で完結する（TMP / パーティクル 等任意）
	/// </summary>
	public interface IPopupView
	{
		GameObject GameObject { get; }

		/// <summary>
		/// popup 再生開始。実装は text/color を任意に解釈してよい
		/// （パーティクル版は無視して prefab 固有の演出を流す等）
		/// </summary>
		/// <param name="text">数字や状態文字列。実装は無視可</param>
		/// <param name="color">色ヒント。実装は無視可</param>
		/// <param name="anchor">追従対象（プレイヤー Transform 等）。null 不可</param>
		/// <param name="anchorOffset">アンカーからのワールドオフセット（頭上 + ずらし）</param>
		/// <param name="onFinished">演出終了時に呼ぶコールバック（プール返却用）</param>
		void Play(string text, Color color, Transform anchor, Vector3 anchorOffset, Action<IPopupView> onFinished);
	}
}
