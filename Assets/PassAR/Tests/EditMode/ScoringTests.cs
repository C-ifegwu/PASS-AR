using NUnit.Framework;

namespace PassAR.Tests
{
    public class ScoringTests
    {
        [Test]
        public void NoErrors_GivesFullScore()
        {
            Assert.AreEqual(100, TaskManager.CalculateScore(0, 10));
        }

        [Test]
        public void EachError_CostsThePenalty()
        {
            Assert.AreEqual(70, TaskManager.CalculateScore(3, 10));
        }

        [Test]
        public void ScoreNeverGoesBelowZero()
        {
            Assert.AreEqual(0, TaskManager.CalculateScore(11, 10));
        }

        [Test]
        public void PenaltyComesFromTheArgument()
        {
            Assert.AreEqual(85, TaskManager.CalculateScore(3, 5));
        }
    }
}
