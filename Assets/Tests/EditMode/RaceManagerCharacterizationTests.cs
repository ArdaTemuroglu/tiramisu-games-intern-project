using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace ArcadeRacing.Tests
{
    public sealed class RaceManagerCharacterizationTests :
        RacingCharacterizationTestFixture
    {
        [Test]
        public void RaceManager_StartsInWaitingState()
        {
            RacingFixture fixture = CreateRacingFixture();

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Waiting")
            );
        }

        [TestCase("TimeTrial")]
        [TestCase("DriftChallenge")]
        public void RaceManager_CountdownTransitionsToRacing(
            string modeName
        )
        {
            RacingFixture fixture = CreateRacingFixture(modeName);
            IEnumerator countdown = CreateCountdownEnumerator(fixture);

            Assert.That(countdown.MoveNext(), Is.True);
            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Countdown")
            );

            Assert.That(
                ReadProperty<bool>(fixture.PlayerCar, "ControlEnabled"),
                Is.False
            );

            int safetySteps = 0;

            while (
                ReadProperty<object>(fixture.RaceManager, "State").ToString()
                    != "Racing" &&
                safetySteps < 10
            )
            {
                Assert.That(countdown.MoveNext(), Is.True);
                safetySteps++;
            }

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Racing"),
                "The deterministic countdown steps must reach Racing."
            );

            Assert.That(
                ReadProperty<bool>(fixture.CheckpointManager, "RaceActive"),
                Is.True
            );

            Assert.That(
                ReadProperty<bool>(fixture.LapTimer, "IsRunning"),
                Is.True
            );

            Assert.That(
                ReadProperty<bool>(fixture.PlayerCar, "ControlEnabled"),
                Is.True
            );
        }

        [Test]
        public void RaceManager_IgnoresFinishOutsideRacing()
        {
            RacingFixture fixture = CreateRacingFixture();
            int cashBefore = ReadProperty<int>(fixture.SaveManager, "Cash");

            InvokePublic(fixture.RaceManager, "FinishRace");

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Waiting")
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(cashBefore),
                "Finish outside Racing must not grant a reward."
            );
        }

        [Test]
        public void TimeTrial_FinishesAfterRequiredLapCountAndRewardsOnce()
        {
            RacingFixture fixture = CreateRacingFixture();
            BeginRacing(fixture);
            SubscribeToProgressEvents(fixture);

            CompleteValidLap(fixture);

            Assert.That(
                ReadProperty<int>(fixture.CheckpointManager, "CurrentLap"),
                Is.EqualTo(2)
            );

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Racing"),
                "Time Trial must continue after lap 1."
            );

            CompleteValidLap(fixture);

            Assert.That(
                ReadProperty<int>(fixture.CheckpointManager, "CurrentLap"),
                Is.EqualTo(3)
            );

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Racing"),
                "Time Trial must continue after lap 2."
            );

            SetAutoProperty(fixture.LapTimer, "RaceTime", 42.5f);
            CompleteValidLap(fixture);

            Assert.That(LapCompletedEventCount, Is.EqualTo(3));
            Assert.That(LastCompletedLapNumber, Is.EqualTo(3));
            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Finished")
            );

            Assert.That(
                ReadProperty<int>(fixture.RaceManager, "LastReward"),
                Is.EqualTo(1750),
                "Current policy is 1000 base plus 250 for each of 3 laps."
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(1750)
            );

            Assert.That(
                ReadProperty<float>(
                    fixture.SaveManager,
                    "BestTimeTrialRaceTime"
                ),
                Is.EqualTo(42.5f)
            );

            Assert.That(
                ReadProperty<bool>(
                    fixture.RaceManager,
                    "LastResultWasRecord"
                ),
                Is.True
            );

            InvokePublic(fixture.RaceManager, "FinishRace");

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(1750),
                "Repeated finish calls must not duplicate the reward."
            );
        }

        [Test]
        public void PauseManager_PauseAndResumePreserveRacingState()
        {
            RacingFixture fixture = CreateRacingFixture();
            BeginRacing(fixture);
            Component pauseManager = CreatePauseManager(fixture);

            InvokePublic(pauseManager, "PauseGame");

            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(
                ReadProperty<bool>(pauseManager, "IsPaused"),
                Is.True
            );

            Assert.That(
                ReadProperty<bool>(fixture.RaceManager, "IsPaused"),
                Is.True
            );

            Assert.That(
                ReadProperty<bool>(fixture.PlayerCar, "ControlEnabled"),
                Is.False
            );

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Racing")
            );

            InvokePublic(pauseManager, "ResumeGame");

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(
                ReadProperty<bool>(pauseManager, "IsPaused"),
                Is.False
            );

            Assert.That(
                ReadProperty<bool>(fixture.RaceManager, "IsPaused"),
                Is.False
            );

            Assert.That(
                ReadProperty<bool>(fixture.PlayerCar, "ControlEnabled"),
                Is.True
            );

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Racing")
            );
        }

        [Test]
        public void DriftChallenge_LapCompletionDoesNotFinishRace()
        {
            RacingFixture fixture = CreateRacingFixture("DriftChallenge");
            BeginRacing(fixture, "DriftChallenge");
            SubscribeToProgressEvents(fixture);

            CompleteValidLap(fixture);
            CompleteValidLap(fixture);
            CompleteValidLap(fixture);

            Assert.That(LapCompletedEventCount, Is.EqualTo(3));
            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Racing"),
                "Drift Challenge must not inherit Time Trial's lap finish policy."
            );

            Assert.That(
                ReadProperty<int>(fixture.CheckpointManager, "CurrentLap"),
                Is.EqualTo(4)
            );
        }

        [Test]
        public void DriftChallenge_FinishesWhenChallengeTimerExpires()
        {
            RacingFixture fixture = CreateRacingFixture("DriftChallenge");
            BeginRacing(fixture, "DriftChallenge");

            float duration = (float)fixture.RaceManager.GetType()
                .GetField("driftChallengeDuration", InstanceMembers)
                .GetValue(fixture.RaceManager);

            SetAutoProperty(fixture.LapTimer, "RaceTime", duration);
            InvokePrivate(fixture.RaceManager, "Update");

            Assert.That(
                ReadProperty<object>(fixture.RaceManager, "State").ToString(),
                Is.EqualTo("Finished")
            );

            Assert.That(
                ReadProperty<int>(fixture.RaceManager, "LastReward"),
                Is.EqualTo(1000),
                "A zero-score Drift Challenge currently grants the base reward."
            );
        }
    }
}
