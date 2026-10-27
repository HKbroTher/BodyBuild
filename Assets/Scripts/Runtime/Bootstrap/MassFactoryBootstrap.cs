using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Makes the project playable from any scene: if no GameManager exists after the first scene loads,
    /// one is created and builds the whole placeholder game. Add a <see cref="DisableAutoBootstrap"/>
    /// component to a scene object to opt out (e.g. for test scenes).
    /// </summary>
    public static class MassFactoryBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGame()
        {
            // Test runner scenes create their own GameManager when they need one.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return;
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            if (Object.FindFirstObjectByType<DisableAutoBootstrap>() != null) return;
            new GameObject("MassFactory").AddComponent<GameManager>();
        }
    }
}
