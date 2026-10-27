using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// PS2-style look: renders the 3D camera into a low resolution, point-filtered texture and draws it
    /// full screen behind the IMGUI interface. A clear-only camera keeps the backbuffer clean.
    /// </summary>
    public sealed class RetroRenderer : MonoBehaviour
    {
        public int verticalResolution = 300;
        public bool retroEnabled = true;

        private Camera _camera;
        private RenderTexture _target;
        private Camera _clearCamera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            var clearGo = new GameObject("ClearCamera");
            clearGo.transform.SetParent(transform.parent, false);
            _clearCamera = clearGo.AddComponent<Camera>();
            _clearCamera.cullingMask = 0;
            _clearCamera.clearFlags = CameraClearFlags.SolidColor;
            _clearCamera.backgroundColor = Color.black;
            _clearCamera.depth = _camera.depth - 10;
        }

        private void Update()
        {
            if (!retroEnabled)
            {
                Release();
                return;
            }

            int height = Mathf.Max(120, verticalResolution);
            int width = Mathf.Max(160, Mathf.RoundToInt(height * (Screen.width / (float)Mathf.Max(1, Screen.height))));
            if (_target == null || _target.width != width || _target.height != height)
            {
                Release();
                _target = new RenderTexture(width, height, 24) { filterMode = FilterMode.Point, name = "RetroTarget" };
                _camera.targetTexture = _target;
            }
        }

        private void OnGUI()
        {
            GUI.depth = 1000; // draw below the gameplay UI
            if (!retroEnabled || _target == null || Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _target, ScaleMode.StretchToFill, false);
        }

        private void Release()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}
