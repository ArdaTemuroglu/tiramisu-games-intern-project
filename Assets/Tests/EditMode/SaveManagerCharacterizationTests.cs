using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcadeRacing.Tests
{
    public sealed class SaveManagerCharacterizationTests :
        ProgressionCharacterizationTestFixture
    {
        [Test]
        public void SaveManager_PersistsCashAndRecordsAcrossLoad()
        {
            string key = CreateTestKey();
            Component writer = CreateSaveManager(key);

            InvokePublic(writer, "AddCash", 2500);
            InvokePublic(writer, "TrySetBestTimeTrial", 80.25f);
            InvokePublic(writer, "TrySetBestDriftScore", 5000);
            InvokePublic(writer, "Save");

            Component reader = CreateSaveManager(key);

            Assert.That(
                ReadProperty<int>(reader, "Cash"),
                Is.EqualTo(2500)
            );

            Assert.That(
                ReadProperty<float>(reader, "BestTimeTrialRaceTime"),
                Is.EqualTo(80.25f).Within(0.0001f)
            );

            Assert.That(
                ReadProperty<int>(reader, "BestDriftScore"),
                Is.EqualTo(5000)
            );
        }

        [Test]
        public void SaveManager_UpdatesBestTimeOnlyWhenImproved()
        {
            string key = CreateTestKey();
            Component saveManager = CreateSaveManager(key);

            Assert.That(
                InvokePublic(
                    saveManager,
                    "TrySetBestTimeTrial",
                    80f
                ),
                Is.EqualTo(true)
            );

            Assert.That(
                InvokePublic(
                    saveManager,
                    "TrySetBestTimeTrial",
                    95f
                ),
                Is.EqualTo(false)
            );

            Assert.That(
                ReadProperty<float>(
                    saveManager,
                    "BestTimeTrialRaceTime"
                ),
                Is.EqualTo(80f).Within(0.0001f)
            );

            Assert.That(
                InvokePublic(
                    saveManager,
                    "TrySetBestTimeTrial",
                    75f
                ),
                Is.EqualTo(true)
            );

            InvokePublic(saveManager, "Save");
            Component reader = CreateSaveManager(key);

            Assert.That(
                ReadProperty<float>(reader, "BestTimeTrialRaceTime"),
                Is.EqualTo(75f).Within(0.0001f)
            );
        }

        [Test]
        public void SaveManager_UpdatesBestDriftOnlyWhenImproved()
        {
            string key = CreateTestKey();
            Component saveManager = CreateSaveManager(key);

            Assert.That(
                InvokePublic(
                    saveManager,
                    "TrySetBestDriftScore",
                    5000
                ),
                Is.EqualTo(true)
            );

            Assert.That(
                InvokePublic(
                    saveManager,
                    "TrySetBestDriftScore",
                    3500
                ),
                Is.EqualTo(false)
            );

            Assert.That(
                ReadProperty<int>(saveManager, "BestDriftScore"),
                Is.EqualTo(5000)
            );

            Assert.That(
                InvokePublic(
                    saveManager,
                    "TrySetBestDriftScore",
                    7000
                ),
                Is.EqualTo(true)
            );

            InvokePublic(saveManager, "Save");
            Component reader = CreateSaveManager(key);

            Assert.That(
                ReadProperty<int>(reader, "BestDriftScore"),
                Is.EqualTo(7000)
            );
        }

        [Test]
        public void SaveManager_RecoversMalformedSaveWithDefaults()
        {
            string key = CreateTestKey();
            PlayerPrefs.SetString(key, "{ definitely invalid json");
            PlayerPrefs.Save();

            LogAssert.Expect(
                LogType.Warning,
                new Regex("^Save data could not be loaded:")
            );

            Component saveManager = CreateSaveManager(key);

            Assert.That(ReadProperty<int>(saveManager, "Cash"), Is.Zero);
            Assert.That(
                ReadProperty<float>(
                    saveManager,
                    "BestTimeTrialRaceTime"
                ),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(saveManager, "BestDriftScore"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(saveManager, "EngineLevel"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(saveManager, "TiresLevel"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(saveManager, "NitroLevel"),
                Is.Zero
            );
        }
    }
}
