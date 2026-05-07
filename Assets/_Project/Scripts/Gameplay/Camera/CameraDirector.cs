using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using Unity.Cinemachine;
using UnityEngine;

namespace GamblingAction.Gameplay.CameraFx
{
	public class CameraDirector : MonoBehaviour
	{
		public static CameraDirector Instance { get; private set; }

		[Header("Impulse Sources")]
		[SerializeField] CinemachineImpulseSource hitImpulse;
		[SerializeField] CinemachineImpulseSource clashMomentImpulse;
		[SerializeField] CinemachineImpulseSource clashExplosionImpulse;
		[SerializeField] CinemachineImpulseSource bumpImpulse;

		void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
		}

		void OnEnable()
		{
			var state = GameStateLocator.Current;
			if (state != null) state.OnGameEvents += HandleEvents;
		}

		void OnDisable()
		{
			var state = GameStateLocator.Current;
			if (state != null) state.OnGameEvents -= HandleEvents;
			if (Instance == this) Instance = null;
		}

		void HandleEvents(EventDto[] events)
		{
			if (events == null) return;
			foreach (var ev in events)
			{
				switch (ev.Type)
				{
					case EventTypes.ClashMoment:    OnClashMoment(); break;
					case EventTypes.ClashExplosion: OnClashExplosion(); break;
					case EventTypes.Hit:            OnHit(); break;
					case EventTypes.Vfx:
						if (ev.VfxType == VfxTypes.Bump) OnBump();
						break;
				}
			}
		}

		public void OnHit()            { if (hitImpulse) hitImpulse.GenerateImpulse(); }
		public void OnClashMoment()    { if (clashMomentImpulse) clashMomentImpulse.GenerateImpulse(); }
		public void OnClashExplosion() { if (clashExplosionImpulse) clashExplosionImpulse.GenerateImpulse(); }
		public void OnBump()           { if (bumpImpulse) bumpImpulse.GenerateImpulse(); }
	}
}
