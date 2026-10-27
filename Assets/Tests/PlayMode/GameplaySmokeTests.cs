using System.Collections;
using MassFactory.Core;
using MassFactory.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MassFactory.Tests
{
    /// <summary>Boots the procedural scene and plays a few actions through the real GameManager.</summary>
    public class GameplaySmokeTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator GameManager_StartsRun_InjectsAndSubmits()
        {
            _root = new GameObject("MassFactoryTest");
            var game = _root.AddComponent<GameManager>();
            game.fixedSeed = 1234;
            game.fastAnimations = true;
            game.persistRuns = false;
            yield return null;

            game.NewRun();
            Assert.AreEqual(GameManager.AppScreen.Playing, game.CurrentScreen);
            Assert.IsNotNull(game.Session);
            yield return WaitIdle(game);

            int injectionsBefore = game.Session.State.Stats.Injections;
            game.Inject();
            Assert.IsTrue(game.Busy, "injection animation started");
            game.Inject(); // ignored while busy
            yield return WaitIdle(game);
            Assert.AreEqual(injectionsBefore + 1, game.Session.State.Stats.Injections);

            if (game.Session.SelectedAthlete != null && !game.Session.IsRunOver)
            {
                long before = game.Session.Competition.SubmittedScore;
                game.Submit();
                yield return WaitIdle(game);
                Assert.Greater(game.Session.Competition.SubmittedScore, before);
            }
        }

        private static IEnumerator WaitIdle(GameManager game)
        {
            // Wait until the game has been idle for a few frames (a destroyed athlete triggers a follow-up swap).
            float timeout = Time.realtimeSinceStartup + 10f;
            int idleFrames = 0;
            while (idleFrames < 3 && Time.realtimeSinceStartup < timeout)
            {
                idleFrames = game.Busy ? 0 : idleFrames + 1;
                yield return null;
            }
            Assert.IsFalse(game.Busy, "animation finished");
        }
    }
}
