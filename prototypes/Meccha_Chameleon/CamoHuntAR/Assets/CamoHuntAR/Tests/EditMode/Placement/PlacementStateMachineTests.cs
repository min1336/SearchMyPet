using System.Collections.Generic;
using NUnit.Framework;

namespace CamoHuntAR.Tests
{
    public sealed class PlacementStateMachineTests
    {
        [Test]
        public void StartsInInitializing()
        {
            Assert.That(new PlacementStateMachine().Current, Is.EqualTo(PlacementState.Initializing));
        }

        [Test]
        public void ReadyPreviewAndPlacedFollowTheHappyPath()
        {
            var machine = new PlacementStateMachine();

            Assert.That(machine.MarkSessionReady(), Is.True);
            Assert.That(machine.MarkPreviewAvailable(), Is.True);
            Assert.That(machine.MarkPlaced(), Is.True);
            Assert.That(machine.Current, Is.EqualTo(PlacementState.Placed));
        }

        [Test]
        public void CannotPlaceBeforePreviewExists()
        {
            var machine = new PlacementStateMachine();
            machine.MarkSessionReady();

            Assert.That(machine.MarkPlaced(), Is.False);
            Assert.That(machine.Current, Is.EqualTo(PlacementState.Detecting));
        }

        [Test]
        public void InvalidTransitionsLeaveStateUnchanged()
        {
            var machine = new PlacementStateMachine();

            Assert.That(machine.MarkPreviewAvailable(), Is.False);
            Assert.That(machine.MarkPlaced(), Is.False);
            Assert.That(machine.Current, Is.EqualTo(PlacementState.Initializing));

            Assert.That(machine.MarkSessionReady(), Is.True);
            Assert.That(machine.MarkPlaced(), Is.False);
            Assert.That(machine.Current, Is.EqualTo(PlacementState.Detecting));

            Assert.That(machine.MarkPreviewAvailable(), Is.True);
            Assert.That(machine.MarkSessionReady(), Is.False);
            Assert.That(machine.Current, Is.EqualTo(PlacementState.Previewing));
        }

        [Test]
        public void ChangedOnlyFiresWhenTheStateActuallyChanges()
        {
            var machine = new PlacementStateMachine();
            var observedStates = new List<PlacementState>();
            machine.Changed += observedStates.Add;

            machine.MarkSessionReady();
            machine.MarkPreviewAvailable();
            machine.MarkPreviewAvailable();
            machine.MarkPlaced();
            machine.MarkPlaced();

            Assert.That(observedStates, Is.EqualTo(new[]
            {
                PlacementState.Detecting,
                PlacementState.Previewing,
                PlacementState.Placed
            }));
        }

        [TestCase(PlacementState.Previewing)]
        [TestCase(PlacementState.Placed)]
        public void ResetReturnsToDetecting(PlacementState state)
        {
            var machine = new PlacementStateMachine();
            machine.MarkSessionReady();
            machine.MarkPreviewAvailable();
            if (state == PlacementState.Placed)
                machine.MarkPlaced();

            machine.Reset();

            Assert.That(machine.Current, Is.EqualTo(PlacementState.Detecting));
        }
    }
}
