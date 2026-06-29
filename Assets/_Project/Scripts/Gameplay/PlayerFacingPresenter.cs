using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay
{
    public class PlayerFacingPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerFacingBodyView m_BodyView;

        private readonly PlayerFacingTracker m_Tracker = new();

        private PlayerView m_PlayerView;
        private IGameState m_State;
        private bool m_IsInitialized;
        private bool m_IsLocalPlayer;
        private bool m_IsP2View;

        private void Awake()
        {
            m_PlayerView = GetComponent<PlayerView>();

            if (m_BodyView == null)
            {
                m_BodyView = GetComponentInChildren<PlayerFacingBodyView>(true);
            }

            if (m_BodyView != null)
            {
                m_BodyView.SetVisible(false);
            }
        }

        private void Start()
        {
            m_State = GameStateLocator.Current;
            if (m_State == null)
            {
                Debug.LogWarning("[PlayerFacingPresenter] GameStateLocator.Current is null.");
                return;
            }

            m_State.OnPlayersChanged += HandlePlayersChanged;
            m_State.OnPhaseChanged += HandlePhaseChanged;
            LocalIntentBus.OnChanged += HandleLocalIntentChanged;

            TryInitialize();
        }

        private void OnDestroy()
        {
            LocalIntentBus.OnChanged -= HandleLocalIntentChanged;

            if (m_State == null) return;

            m_State.OnPlayersChanged -= HandlePlayersChanged;
            m_State.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePlayersChanged()
        {
            TryInitialize();
            if (!m_IsInitialized) return;
            if (!m_IsLocalPlayer) return;

            RefreshVisual();
        }

        private void HandlePhaseChanged(EGamePhase phase)
        {
            TryInitialize();
            if (!m_IsInitialized) return;
            if (!m_IsLocalPlayer) return;

            if (phase == EGamePhase.Countdown)
            {
                m_Tracker.ResetToDefault(GetDefaultFacingDir());
                ApplyBodyVisual();
            }
        }

        private void HandleLocalIntentChanged()
        {
            TryInitialize();
            if (!m_IsInitialized) return;
            if (!m_IsLocalPlayer) return;

            RefreshFacingOnly();
        }

        private void TryInitialize()
        {
            if (m_IsInitialized) return;
            if (m_PlayerView == null) return;
            if (m_State == null) return;
            if (string.IsNullOrEmpty(m_PlayerView.PlayerId)) return;
            if (string.IsNullOrEmpty(m_State.MyId)) return;

            m_IsLocalPlayer = m_PlayerView.PlayerId == m_State.MyId;

            PlayerDto me = m_State.Me;
            m_IsP2View = me != null && me.Role == Roles.P2;

            m_Tracker.Initialize(GetDefaultFacingDir());
            m_IsInitialized = true;

            if (!m_IsLocalPlayer) return;

            RefreshVisual();
        }

        private void RefreshVisual()
        {
            RefreshFacingOnly();
            RefreshColorAndVisibility();
        }

        private void RefreshFacingOnly()
        {
            if (m_BodyView == null) return;

            string dir = ResolveFacingDir();
            m_Tracker.ApplyIntentDir(dir);
            ApplyBodyVisual();
        }

        private void RefreshColorAndVisibility()
        {
            if (m_BodyView == null) return;

            if (!TryResolvePlayerColor(out Color color))
            {
                m_BodyView.SetVisible(false);
                return;
            }

            m_BodyView.SetColor(color);
            m_BodyView.SetVisible(m_IsLocalPlayer);
        }

        private string ResolveFacingDir()
        {
            string localDir = LocalIntentBus.Current != null ? LocalIntentBus.Current.Dir : null;
            if (!string.IsNullOrEmpty(localDir))
            {
                return localDir;
            }

            if (m_State != null && m_State.Players.TryGetValue(m_PlayerView.PlayerId, out PlayerDto dto))
            {
                string syncedDir = dto.Intent != null ? dto.Intent.Dir : null;
                if (!string.IsNullOrEmpty(syncedDir))
                {
                    return syncedDir;
                }
            }

            return null;
        }

        private bool TryResolvePlayerColor(out Color color)
        {
            color = Color.white;

            if (m_State == null) return false;
            if (m_PlayerView == null) return false;
            if (!m_State.Players.TryGetValue(m_PlayerView.PlayerId, out PlayerDto dto)) return false;
            if (string.IsNullOrEmpty(dto.Color)) return false;

            return ColorUtility.TryParseHtmlString(dto.Color, out color);
        }

        private void ApplyBodyVisual()
        {
            if (m_BodyView == null) return;
            if (!m_IsLocalPlayer) return;

            m_BodyView.SetDirection(m_Tracker.CurrentFacingDir, m_IsP2View);
        }

        private string GetDefaultFacingDir()
        {
            return m_IsP2View ? Directions.Down : Directions.Up;
        }
    }
}