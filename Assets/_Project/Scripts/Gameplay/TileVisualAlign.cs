using UnityEngine;

namespace GamblingAction.Gameplay
{
	/// <summary>
	/// </summary>
	[ExecuteAlways]
	public class TileVisualAlign : MonoBehaviour
	{
		void OnEnable() => Apply();

		void Update()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) Apply();
#endif
		}

		void Apply()
		{
			var p = transform.localPosition;
			p.y = -transform.localScale.y * 0.5f;
			transform.localPosition = p;
		}
	}
}
