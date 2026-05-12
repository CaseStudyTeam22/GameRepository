using UnityEngine;

namespace GamblingAction.Gameplay.CameraFx.Dev
{
	public class CameraDebugPanel : MonoBehaviour
	{
		void OnGUI()
		{
			var d = CameraDirector.Instance;
			if (d == null) { GUI.Label(new Rect(10, 10, 300, 24), "CameraDirector.Instance == null"); return; }

			GUI.Box(new Rect(8, 8, 180, 200), "Camera FX Debug");
			if (GUI.Button(new Rect(16, 36,  160, 32), "Hit"))             d.OnHit();
			if (GUI.Button(new Rect(16, 72,  160, 32), "Clash Moment"))    d.OnClashMoment();
			if (GUI.Button(new Rect(16, 108, 160, 32), "Clash Explosion")) d.OnClashExplosion();
			if (GUI.Button(new Rect(16, 144, 160, 32), "Bump"))            d.OnBump();
		}
	}
}
