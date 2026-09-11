using System;
using UnityEngine;

namespace ArcadeRacing
{
    [Serializable]
    public sealed class SaveData
    {
        public int cash;
        public int engineLevel;
        public int tiresLevel;
        public int nitroLevel;
        public float bestTimeTrialRaceTime;
        public int bestDriftScore;
    }

    public sealed class SaveManager : MonoBehaviour
    {
        private const int BaseUpgradeLevel = 0;

        private static bool sessionUpgradeLevelsInitialized;
        private static int sessionEngineLevel;
        private static int sessionTiresLevel;
        private static int sessionNitroLevel;

        [SerializeField] private string saveKey = "ArcadeRacingSaveV1";
        [SerializeField] private bool loadOnAwake = true;
        [SerializeField] private bool saveOnApplicationPause = true;

        private SaveData data = new SaveData();

        public int Cash => data.cash;
        public int EngineLevel => data.engineLevel;
        public int TiresLevel => data.tiresLevel;
        public int NitroLevel => data.nitroLevel;
        public float BestTimeTrialRaceTime => data.bestTimeTrialRaceTime;
        public int BestDriftScore => data.bestDriftScore;
        public bool IsLoaded { get; private set; }

        public event Action DataChanged;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration
        )]
        private static void BeginNewApplicationSession()
        {
            sessionEngineLevel = BaseUpgradeLevel;
            sessionTiresLevel = BaseUpgradeLevel;
            sessionNitroLevel = BaseUpgradeLevel;
            sessionUpgradeLevelsInitialized = true;
        }

        private void Awake()
        {
            if (loadOnAwake)
            {
                Load();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && saveOnApplicationPause)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(saveKey))
            {
                data = new SaveData();
                ApplySessionUpgradeLevels();
                IsLoaded = true;
                DataChanged?.Invoke();
                return;
            }

            string json = PlayerPrefs.GetString(saveKey, string.Empty);

            try
            {
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                data = loaded ?? new SaveData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Save data could not be loaded: {exception.Message}",
                    this
                );

                data = new SaveData();
            }

            Sanitize();
            ApplySessionUpgradeLevels();
            IsLoaded = true;
            DataChanged?.Invoke();
        }

        public void Save()
        {
            Sanitize();
            SaveData persistentData = new SaveData
            {
                cash = data.cash,
                engineLevel = BaseUpgradeLevel,
                tiresLevel = BaseUpgradeLevel,
                nitroLevel = BaseUpgradeLevel,
                bestTimeTrialRaceTime = data.bestTimeTrialRaceTime,
                bestDriftScore = data.bestDriftScore
            };

            string json = JsonUtility.ToJson(persistentData);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();
        }

        public void AddCash(int amount)
        {
            data.cash = Mathf.Max(
                0,
                data.cash + Mathf.Max(0, amount)
            );

            DataChanged?.Invoke();
        }

        public bool SpendCash(int amount)
        {
            int safeAmount = Mathf.Max(0, amount);

            if (data.cash < safeAmount)
            {
                return false;
            }

            data.cash -= safeAmount;
            DataChanged?.Invoke();
            return true;
        }

        public bool TrySetBestTimeTrial(float raceTime)
        {
            if (raceTime <= 0f)
            {
                return false;
            }

            bool isRecord =
                data.bestTimeTrialRaceTime <= 0f ||
                raceTime < data.bestTimeTrialRaceTime;

            if (!isRecord)
            {
                return false;
            }

            data.bestTimeTrialRaceTime = raceTime;
            DataChanged?.Invoke();
            return true;
        }

        public bool TrySetBestDriftScore(int score)
        {
            int safeScore = Mathf.Max(0, score);

            if (safeScore <= data.bestDriftScore)
            {
                return false;
            }

            data.bestDriftScore = safeScore;
            DataChanged?.Invoke();
            return true;
        }

        public int GetUpgradeLevel(UpgradeType type)
        {
            if (type == UpgradeType.Engine)
            {
                return data.engineLevel;
            }

            if (type == UpgradeType.Tires)
            {
                return data.tiresLevel;
            }

            return data.nitroLevel;
        }

        public void IncrementUpgradeLevel(UpgradeType type)
        {
            if (type == UpgradeType.Engine)
            {
                data.engineLevel++;
            }
            else if (type == UpgradeType.Tires)
            {
                data.tiresLevel++;
            }
            else
            {
                data.nitroLevel++;
            }

            CaptureSessionUpgradeLevels();
            DataChanged?.Invoke();
        }

        public void ResetSave()
        {
            data = new SaveData();
            sessionEngineLevel = BaseUpgradeLevel;
            sessionTiresLevel = BaseUpgradeLevel;
            sessionNitroLevel = BaseUpgradeLevel;
            sessionUpgradeLevelsInitialized = true;
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            IsLoaded = true;
            DataChanged?.Invoke();
        }

        private void Sanitize()
        {
            data.cash = Mathf.Max(0, data.cash);
            data.engineLevel = Mathf.Max(0, data.engineLevel);
            data.tiresLevel = Mathf.Max(0, data.tiresLevel);
            data.nitroLevel = Mathf.Max(0, data.nitroLevel);
            data.bestDriftScore = Mathf.Max(0, data.bestDriftScore);

            if (
                float.IsNaN(data.bestTimeTrialRaceTime) ||
                float.IsInfinity(data.bestTimeTrialRaceTime) ||
                data.bestTimeTrialRaceTime < 0f
            )
            {
                data.bestTimeTrialRaceTime = 0f;
            }
        }

        private void ApplySessionUpgradeLevels()
        {
            EnsureSessionUpgradeLevelsInitialized();
            data.engineLevel = sessionEngineLevel;
            data.tiresLevel = sessionTiresLevel;
            data.nitroLevel = sessionNitroLevel;
        }

        private void CaptureSessionUpgradeLevels()
        {
            sessionEngineLevel = Mathf.Max(
                BaseUpgradeLevel,
                data.engineLevel
            );

            sessionTiresLevel = Mathf.Max(
                BaseUpgradeLevel,
                data.tiresLevel
            );

            sessionNitroLevel = Mathf.Max(
                BaseUpgradeLevel,
                data.nitroLevel
            );

            sessionUpgradeLevelsInitialized = true;
        }

        private static void EnsureSessionUpgradeLevelsInitialized()
        {
            if (!sessionUpgradeLevelsInitialized)
            {
                BeginNewApplicationSession();
            }
        }
    }
}
