using NUnit.Framework;

namespace ArcadeRacing.Tests
{
    public sealed class CheckpointManagerCharacterizationTests :
        RacingCharacterizationTestFixture
    {
        [Test]
        public void CheckpointManager_BeginsWithCheckpointOneExpected()
        {
            RacingFixture fixture = CreateRacingFixture();

            InvokePublic(fixture.CheckpointManager, "BeginRace");

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "ExpectedCheckpointNumber"
                ),
                Is.EqualTo(1),
                "A new race should expect checkpoint 1."
            );

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "CompletedCheckpointCount"
                ),
                Is.Zero
            );

            Assert.That(
                ReadProperty<bool>(fixture.CheckpointManager, "RaceActive"),
                Is.True
            );
        }

        [Test]
        public void CheckpointManager_IgnoresDuplicateCheckpoint()
        {
            RacingFixture fixture = CreateRacingFixture();
            BeginRacing(fixture);
            SubscribeToProgressEvents(fixture);

            PassCheckpoint(fixture, fixture.LapCheckpoints[0]);
            PassCheckpoint(fixture, fixture.LapCheckpoints[0]);

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "CompletedCheckpointCount"
                ),
                Is.EqualTo(1),
                "Passing checkpoint 1 twice must not advance to checkpoint 3."
            );

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "ExpectedCheckpointNumber"
                ),
                Is.EqualTo(2)
            );

            Assert.That(
                CheckpointEventCount,
                Is.EqualTo(1),
                "A duplicate trigger must not emit another checkpoint event."
            );

            Assert.That(LapCompletedEventCount, Is.Zero);
        }

        [Test]
        public void CheckpointManager_DoesNotEmitProgressForOutOfOrderCheckpoint()
        {
            RacingFixture fixture = CreateRacingFixture();
            BeginRacing(fixture);
            SubscribeToProgressEvents(fixture);

            PassCheckpoint(fixture, fixture.LapCheckpoints[1]);

            Assert.That(CheckpointEventCount, Is.Zero);
            Assert.That(LapCompletedEventCount, Is.Zero);
            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "CompletedCheckpointCount"
                ),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "ExpectedCheckpointNumber"
                ),
                Is.EqualTo(1),
                "An out-of-order checkpoint must leave checkpoint 1 expected."
            );
        }

        [Test]
        public void CheckpointManager_DoesNotCompleteLapBeforeAllCheckpoints()
        {
            RacingFixture fixture = CreateRacingFixture();
            BeginRacing(fixture);
            SubscribeToProgressEvents(fixture);

            PassCheckpoint(fixture, fixture.LapCheckpoints[0]);
            PassCheckpoint(fixture, fixture.StartFinish);

            Assert.That(LapCompletedEventCount, Is.Zero);
            Assert.That(CheckpointEventCount, Is.EqualTo(1));
            Assert.That(
                ReadProperty<int>(fixture.CheckpointManager, "CurrentLap"),
                Is.EqualTo(1),
                "Early StartFinish must not complete the current lap."
            );

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "ExpectedCheckpointNumber"
                ),
                Is.EqualTo(2)
            );
        }

        [Test]
        public void CheckpointManager_DoesNotCompleteLapWhenCheckpointIsMissing()
        {
            RacingFixture fixture = CreateRacingFixture();
            BeginRacing(fixture);
            SubscribeToProgressEvents(fixture);

            PassCheckpoint(fixture, fixture.LapCheckpoints[0]);
            PassCheckpoint(fixture, fixture.LapCheckpoints[1]);
            PassCheckpoint(fixture, fixture.LapCheckpoints[3]);
            PassCheckpoint(fixture, fixture.StartFinish);

            Assert.That(LapCompletedEventCount, Is.Zero);
            Assert.That(CheckpointEventCount, Is.EqualTo(2));
            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "CompletedCheckpointCount"
                ),
                Is.EqualTo(2)
            );

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "ExpectedCheckpointNumber"
                ),
                Is.EqualTo(3),
                "Skipping checkpoint 3 must leave checkpoint 3 expected."
            );
        }

        [Test]
        public void CheckpointManager_CompletesLapAfterValidSequence()
        {
            RacingFixture fixture = CreateRacingFixture();
            BeginRacing(fixture);
            SubscribeToProgressEvents(fixture);

            for (int i = 0; i < fixture.LapCheckpoints.Length; i++)
            {
                PassCheckpoint(fixture, fixture.LapCheckpoints[i]);
            }

            Assert.That(
                ReadProperty<bool>(
                    fixture.CheckpointManager,
                    "WaitingForFinish"
                ),
                Is.True
            );

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "ExpectedCheckpointNumber"
                ),
                Is.Zero,
                "Zero identifies StartFinish as the expected gate."
            );

            Assert.That(CheckpointEventCount, Is.EqualTo(4));
            Assert.That(LastCheckpointProgress, Is.EqualTo(4));
            Assert.That(LastCheckpointTotal, Is.EqualTo(4));

            PassCheckpoint(fixture, fixture.StartFinish);

            Assert.That(LapCompletedEventCount, Is.EqualTo(1));
            Assert.That(LastCompletedLapNumber, Is.EqualTo(1));
            Assert.That(
                ReadProperty<int>(fixture.CheckpointManager, "CurrentLap"),
                Is.EqualTo(2)
            );

            Assert.That(
                ReadProperty<int>(
                    fixture.CheckpointManager,
                    "ExpectedCheckpointNumber"
                ),
                Is.EqualTo(1),
                "A valid lap must reset the sequence to checkpoint 1."
            );
        }
    }
}
