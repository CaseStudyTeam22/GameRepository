using UnityEngine;

namespace GamblingAction.Gameplay.PopupFx.Dev
{
	public class PopupDebugPanel : MonoBehaviour
	{
		[Header("Debug Anchors")]
		[Tooltip("Sandbox 用ダミーアンカー。空なら自身の transform を使う")]
		[SerializeField] private Transform m_AnchorP1;
		[SerializeField] private Transform m_AnchorP2;

		private const string DummyP1 = "debug_p1";
		private const string DummyP2 = "debug_p2";

		private void Start()
		{
			var d = PopupDirector.Instance;
			if (d == null) return;
			d.RegisterPlayer(DummyP1, m_AnchorP1 != null ? m_AnchorP1 : transform);
			if (m_AnchorP2 != null) d.RegisterPlayer(DummyP2, m_AnchorP2);
		}

		private void OnDestroy()
		{
			var d = PopupDirector.Instance;
			if (d == null) return;
			d.UnregisterPlayer(DummyP1);
			d.UnregisterPlayer(DummyP2);
		}

		private void OnGUI()
		{
			var d = PopupDirector.Instance;
			if (d == null)
			{
				GUI.Label(new Rect(10, 10, 320, 24), "PopupDirector.Instance == null");
				return;
			}

			GUI.Box(new Rect(8, 8, 240, 380), "Popup FX Debug");

			int y = 36;
			if (Btn(y, "+10 chips (P1)"))     { d.SpawnChipsDelta(DummyP1,  10); } y += 32;
			if (Btn(y, "-5 chips (P1)"))      { d.SpawnChipsDelta(DummyP1,  -5); } y += 32;
			if (Btn(y, "Hit -3 (P1)"))        { d.SpawnByKind(DummyP1, PopupDirector.EPopupKind.HitDamage, "-3"); } y += 32;
			if (Btn(y, "KNOCKBACK! (push成立)")) { d.SpawnByKind(DummyP1, PopupDirector.EPopupKind.PushedAttack, "KNOCKBACK!"); } y += 32;
			if (Btn(y, "PUSHED! (bump)"))     { d.SpawnByKind(DummyP1, PopupDirector.EPopupKind.BumpPushed, "PUSHED!"); } y += 32;
			if (Btn(y, "BLOCKED (bump)"))     { d.SpawnByKind(DummyP1, PopupDirector.EPopupKind.BumpBlocked, "BLOCKED"); } y += 32;
			if (Btn(y, "KICKED! (head-on)"))  { d.SpawnByKind(DummyP1, PopupDirector.EPopupKind.BumpKicked, "KICKED!"); } y += 32;
			if (Btn(y, "並発 3 連 (高さずらし)"))
			{
				d.SpawnChipsDelta(DummyP1, 5);
				d.SpawnChipsDelta(DummyP1, 7);
				d.SpawnChipsDelta(DummyP1, -2);
			} y += 32;
			if (Btn(y, "P1 + P2 同時"))
			{
				d.SpawnChipsDelta(DummyP1, 3);
				d.SpawnChipsDelta(DummyP2, -4);
			} y += 32;
		}

		private static bool Btn(int y, string label)
		{
			return GUI.Button(new Rect(16, y, 220, 28), label);
		}
	}
}
