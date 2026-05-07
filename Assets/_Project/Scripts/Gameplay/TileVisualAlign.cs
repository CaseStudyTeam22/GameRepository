using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	[ExecuteAlways]
	public class TileVisualAlign : MonoBehaviour
	{
		private void OnEnable() => Apply();

		private void Update()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) Apply();
#endif
		}

		private void Apply()
		{
			var p = transform.localPosition;
			p.y = -transform.localScale.y * 0.5f;
			transform.localPosition = p;
		}
	}
}
