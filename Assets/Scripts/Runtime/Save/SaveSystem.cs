using System;
using System.IO;
using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Basic save/resume: the whole RunState serialized with JsonUtility to persistentDataPath.</summary>
    public static class SaveSystem
    {
        private const int Version = 1;
        private const string FileName = "massfactory_run.json";

        [Serializable]
        private sealed class SaveFile
        {
            public int version;
            public string savedAtUtc;
            public RunState state;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(FilePath);

        public static void Save(RunState state)
        {
            if (state == null || state.Phase != RunPhase.InProgress) return;
            try
            {
                var file = new SaveFile { version = Version, savedAtUtc = DateTime.UtcNow.ToString("o"), state = state };
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(file));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MassFactory] Could not save run: " + e.Message);
            }
        }

        public static RunState Load()
        {
            try
            {
                if (!HasSave) return null;
                var file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
                if (file == null || file.version != Version || file.state == null || file.state.Phase != RunPhase.InProgress) return null;
                return file.state;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MassFactory] Could not load save, ignoring it: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (HasSave) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MassFactory] Could not delete save: " + e.Message);
            }
        }
    }
}
