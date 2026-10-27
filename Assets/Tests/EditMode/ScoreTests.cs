using MassFactory.Core;
using NUnit.Framework;

namespace MassFactory.Tests
{
    public class ScoreTests
    {
        [TestCase(0, 500)]
        [TestCase(1, 750)]
        [TestCase(2, 1125)]
        [TestCase(3, 1688)]
        [TestCase(4, 2531)]
        [TestCase(5, 3797)]
        [TestCase(6, 5695)]
        [TestCase(7, 8543)]
        [TestCase(8, 12814)]
        [TestCase(9, 19222)]
        [TestCase(10, 28833)]
        public void Score_MatchesSpecTable(int level, long expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.Calculate(500, 1.5, level));
        }

        [Test]
        public void Score_AppliesMultipliers()
        {
            Assert.AreEqual(1125, ScoreCalculator.Calculate(500, 1.5, 1, 1.0, 1.5));
            Assert.AreEqual(1500, ScoreCalculator.Calculate(500, 1.5, 1, 2.0, 1.0));
        }

        [Test]
        public void Score_IsClampedInsteadOfOverflowing()
        {
            Assert.AreEqual(GameConfig.MaxScore, ScoreCalculator.Calculate(500, 1.5, 5000));
            Assert.AreEqual(GameConfig.MaxScore, ScoreCalculator.Calculate(500, 1.5, 200, 1e300, 1e300));
        }

        [Test]
        public void Score_NegativeLevelTreatedAsZero()
        {
            Assert.AreEqual(500, ScoreCalculator.Calculate(500, 1.5, -3));
        }

        [Test]
        public void SaturatingAdd_DoesNotOverflow()
        {
            Assert.AreEqual(GameConfig.MaxScore, ScoreCalculator.SaturatingAdd(GameConfig.MaxScore, GameConfig.MaxScore));
            Assert.AreEqual(30, ScoreCalculator.SaturatingAdd(10, 20));
        }
    }
}
