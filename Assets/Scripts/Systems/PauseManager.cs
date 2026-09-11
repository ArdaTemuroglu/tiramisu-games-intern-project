using UnityEngine;

namespace ArcadeRacing
{
    public sealed class PauseManager : MonoBehaviour
    {
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private bool pauseAudio = true;
        [SerializeField] private bool pauseWhenFocusIsLost = true;

        public bool IsPaused { get; private set; }

        private void Start()
        {
            if (raceManager == null || uiManager == null)
            {
                throw new MissingReferenceException(
                    "PauseManager requires RaceManager and UIManager references."
                );
            }

            ResumeGame();
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (
                !hasFocus &&
                pauseWhenFocusIsLost &&
                !IsPaused &&
                CanPause()
            )
            {
                PauseGame();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public void TogglePause()
        {
            if (IsPaused)
            {
                ResumeGame();
                return;
            }

            PauseGame();
        }

        public void PauseGame()
        {
            if (IsPaused || !CanPause())
            {
                return;
            }

            raceManager.SetPaused(true);
            IsPaused = true;
            Time.timeScale = 0f;

            if (pauseAudio)
            {
                AudioListener.pause = true;
            }

            uiManager.ShowPauseMenu();
        }

        public void ResumeGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (raceManager != null)
            {
                raceManager.SetPaused(false);
            }

            IsPaused = false;

            if (uiManager != null)
            {
                uiManager.HidePauseMenu();
            }
        }

        public void RestartRace()
        {
            ResumeGame();
            raceManager.RestartRace();
        }

        public void QuitGame()
        {
            ResumeGame();
            raceManager.QuitApplication();
        }

        private bool CanPause()
        {
            if (raceManager == null)
            {
                return false;
            }

            return
                raceManager.State == RaceState.Countdown ||
                raceManager.State == RaceState.Racing;
        }
    }
}