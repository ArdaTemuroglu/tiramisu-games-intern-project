using System;
using UnityEngine;

namespace ArcadeRacing
{
    public sealed class LapTimer : MonoBehaviour
    {
        public float RaceTime { get; private set; }
        public float CurrentLapTime { get; private set; }
        public float LastLapTime { get; private set; }
        public float BestLapTime { get; private set; }
        public int CurrentLapNumber { get; private set; } = 1;
        public bool IsRunning { get; private set; }

        public event Action RaceStarted;
        public event Action<int> LapStarted;
        public event Action<int, float, bool> LapCompleted;
        public event Action RaceStopped;

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            RaceTime += Time.deltaTime;
            CurrentLapTime += Time.deltaTime;
        }

        public void BeginRace(float existingBestLapTime = 0f)
        {
            RaceTime = 0f;
            CurrentLapTime = 0f;
            LastLapTime = 0f;
            BestLapTime = Mathf.Max(0f, existingBestLapTime);
            CurrentLapNumber = 1;
            IsRunning = true;

            RaceStarted?.Invoke();
            LapStarted?.Invoke(CurrentLapNumber);
        }

        public float CompleteLap()
        {
            if (!IsRunning)
            {
                return 0f;
            }

            LastLapTime = CurrentLapTime;

            bool isNewBest =
                LastLapTime > 0f &&
                (BestLapTime <= 0f || LastLapTime < BestLapTime);

            if (isNewBest)
            {
                BestLapTime = LastLapTime;
            }

            LapCompleted?.Invoke(
                CurrentLapNumber,
                LastLapTime,
                isNewBest
            );

            CurrentLapTime = 0f;
            return LastLapTime;
        }

        public void BeginNextLap()
        {
            if (!IsRunning)
            {
                return;
            }

            CurrentLapNumber++;
            CurrentLapTime = 0f;
            LapStarted?.Invoke(CurrentLapNumber);
        }

        public void StopRace()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            RaceStopped?.Invoke();
        }

        public void ResetTimers()
        {
            RaceTime = 0f;
            CurrentLapTime = 0f;
            LastLapTime = 0f;
            BestLapTime = 0f;
            CurrentLapNumber = 1;
            IsRunning = false;
        }

        public static string FormatTime(float time)
        {
            int totalMilliseconds = Mathf.Max(
                0,
                Mathf.FloorToInt(time * 1000f)
            );

            int minutes = totalMilliseconds / 60000;
            int seconds = totalMilliseconds % 60000 / 1000;
            int milliseconds = totalMilliseconds % 1000;

            return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
        }
    }
}
