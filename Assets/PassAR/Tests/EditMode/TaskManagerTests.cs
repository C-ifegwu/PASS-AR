using System.Collections.Generic;
using NUnit.Framework;

namespace PassAR.Tests
{
    public class TaskManagerTests
    {
        private class FakeStep : TrainingStep
        {
            public bool Done;
            public bool Revert;
            public bool Spray;
            public int Entered;
            public int Exited;

            public FakeStep(string name, bool spray = false) : base(name)
            {
                Spray = spray;
            }

            public override string Instruction => "do " + stepName;
            public override string WrongOrderMessage => "wrong " + stepName;
            public override bool AllowsSpray => Spray;
            public override bool ShouldRevert => Revert;

            public override void Enter()
            {
                base.Enter();
                Entered++;
            }

            public override void Exit()
            {
                Exited++;
            }

            public override bool IsComplete()
            {
                return Done;
            }
        }

        private static TaskManager Start(out List<FakeStep> steps)
        {
            steps = new List<FakeStep>
            {
                new FakeStep("pull"),
                new FakeStep("aim"),
                new FakeStep("squeeze", true),
                new FakeStep("sweep", true)
            };
            var manager = new TaskManager(10);
            manager.Begin(new List<TrainingStep>(steps));
            return manager;
        }

        [Test]
        public void Begin_EntersTheFirstStep()
        {
            TaskManager manager = Start(out var steps);
            Assert.AreSame(steps[0], manager.CurrentStep);
            Assert.AreEqual(1, steps[0].Entered);
        }

        [Test]
        public void CompletedStep_MovesToTheNext()
        {
            TaskManager manager = Start(out var steps);
            steps[0].Done = true;
            manager.Tick(0.1f);
            Assert.AreSame(steps[1], manager.CurrentStep);
            Assert.AreEqual(1, steps[0].Exited);
        }

        [Test]
        public void Steps_AreNeverSkipped()
        {
            TaskManager manager = Start(out var steps);
            steps[2].Done = true; // a later step saying it is done must not matter
            manager.Tick(0.1f);
            Assert.AreSame(steps[0], manager.CurrentStep);
        }

        [Test]
        public void LastStepCompleting_RaisesCompletedOnce()
        {
            TaskManager manager = Start(out var steps);
            int raised = 0;
            manager.Completed += () => raised++;

            foreach (FakeStep step in steps)
            {
                step.Done = true;
                manager.Tick(0.1f);
            }
            manager.Tick(0.1f);

            Assert.IsTrue(manager.IsCompleted);
            Assert.AreEqual(1, raised);
            Assert.IsNull(manager.CurrentStep);
        }

        [Test]
        public void Squeeze_BeforeSprayStep_IsIgnoredAndCountsAsOneError()
        {
            TaskManager manager = Start(out _);
            string message = null;
            manager.ErrorReported += m => message = m;

            Assert.IsFalse(manager.TrySqueeze());
            Assert.AreEqual(1, manager.Errors);
            Assert.AreEqual("wrong pull", message);
        }

        [Test]
        public void Squeeze_DuringSprayStep_IsAllowedWithoutError()
        {
            TaskManager manager = Start(out var steps);
            steps[0].Done = true; manager.Tick(0.1f);
            steps[1].Done = true; manager.Tick(0.1f);

            Assert.IsTrue(manager.SprayAllowed);
            Assert.IsTrue(manager.TrySqueeze());
            Assert.AreEqual(0, manager.Errors);
        }

        [Test]
        public void RevertRequest_GoesBackOneStep()
        {
            TaskManager manager = Start(out var steps);
            steps[0].Done = true; manager.Tick(0.1f);
            steps[1].Done = true; manager.Tick(0.1f);
            Assert.AreSame(steps[2], manager.CurrentStep);

            steps[2].Revert = true;
            manager.Tick(0.1f);

            Assert.AreSame(steps[1], manager.CurrentStep);
            Assert.AreEqual(2, steps[1].Entered);
        }

        [Test]
        public void BuildRecord_ReportsTimeErrorsAndScore()
        {
            TaskManager manager = Start(out var steps);
            manager.Tick(1f);
            manager.ReportError("x");
            manager.ReportError("y");
            foreach (FakeStep step in steps)
            {
                step.Done = true;
                manager.Tick(0.5f);
            }

            SessionRecord record = manager.BuildRecord();

            Assert.IsTrue(record.completed);
            Assert.AreEqual(2, record.errors);
            Assert.AreEqual(80, record.score);
            Assert.AreEqual(4, record.stepTimes.Length);
            Assert.AreEqual(3.0f, record.durationSec, 0.001f);
            Assert.AreEqual(1.5f, record.stepTimes[0], 0.001f);
        }
    }
}
