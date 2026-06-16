using GamblingAction.Core.Dto;

namespace GamblingAction.Gameplay
{
    public sealed class PlayerFacingTracker
    {
        private string m_CurrentFacingDir = Directions.Up;

        public string CurrentFacingDir => m_CurrentFacingDir;

        public void Initialize(string defaultDir = Directions.Up)
        {
            m_CurrentFacingDir = string.IsNullOrEmpty(defaultDir) ? Directions.Up : defaultDir;
        }

        public void ResetToDefault(string defaultDir = Directions.Up)
        {
            m_CurrentFacingDir = string.IsNullOrEmpty(defaultDir) ? Directions.Up : defaultDir;
        }

        public void ApplyIntentDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            m_CurrentFacingDir = dir;
        }
    }
}