using GamblingAction.Domain;
using Unity.Cinemachine;
using UnityEngine;

namespace GamblingAction.Gameplay.CameraFx
{
	// 自分が P1 / P2 どちらでも「自分が画面左下」に見えるよう、
	// role に応じてデフォルトの CinemachineCamera を選ぶ。
	// 起動時は両プレイヤー共通の m_StartCamera から始まり、
	// role 確定後に P1 / P2 カメラへブレンドする。
	public class CameraViewpointSelector : MonoBehaviour
	{
		[SerializeField, Tooltip("両プレイヤー共通の開始カメラ。Priority を P1/P2 より高めに置いておく")]
		private CinemachineCamera m_StartCamera;
		[SerializeField, Tooltip("P1 視点のカメラ。P1 の出生側（盤の左下）を正面に見る位置に配置")]
		private CinemachineCamera m_P1Camera;
		[SerializeField, Tooltip("P2 視点のカメラ。P1 視点を盤中心まわりに 180° 回した位置に配置")]
		private CinemachineCamera m_P2Camera;

		[SerializeField, Tooltip("選ばれた側に設定する Priority。m_StartCamera より大きい値にすること")]
		private int m_ActivePriority = 30;

		private IGameState m_State;

		private void Start()
		{
			m_State = GameStateLocator.Current;
			if (m_State == null)
			{
				Debug.LogError("[CameraViewpoint] GameStateLocator.Current is null");
				return;
			}

			m_State.OnStateInitialized += ApplyViewpoint;
			ApplyViewpoint();
		}

		private void OnDestroy()
		{
			if (m_State != null) m_State.OnStateInitialized -= ApplyViewpoint;
		}

		private void ApplyViewpoint()
		{
			var me = m_State.Me;
			if (me == null) return;
			if (m_P1Camera == null || m_P2Camera == null)
			{
				Debug.LogWarning("[CameraViewpoint] P1Camera / P2Camera が未設定");
				return;
			}

			bool useP2 = me.Role == "P2";
			m_P1Camera.Priority = useP2 ? 0 : m_ActivePriority;
			m_P2Camera.Priority = useP2 ? m_ActivePriority : 0;
		}
	}
}
