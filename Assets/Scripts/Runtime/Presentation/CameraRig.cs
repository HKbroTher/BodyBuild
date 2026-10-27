using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Fixed presentation camera with screen shake and a small suspense zoom.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Vector3 basePosition = new Vector3(0f, 1.45f, -4.6f);
        public Vector3 lookAt = new Vector3(0f, 1.15f, 0f);
        public float baseFieldOfView = 42f;

        private Camera _camera;
        private float _shakeTime;
        private float _shakeStrength;
        private float _zoom;
        private float _zoomTarget;

        public Camera Camera => _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        public void Shake(float strength, float duration)
        {
            _shakeStrength = Mathf.Max(_shakeStrength, strength);
            _shakeTime = Mathf.Max(_shakeTime, duration);
        }

        /// <summary>0 = normal framing, 1 = tight on the athlete.</summary>
        public void SetZoom(float amount)
        {
            _zoomTarget = Mathf.Clamp01(amount);
        }

        private void LateUpdate()
        {
            _zoom = Mathf.MoveTowards(_zoom, _zoomTarget, Time.deltaTime * 2.5f);
            var offset = Vector3.zero;
            if (_shakeTime > 0)
            {
                _shakeTime -= Time.deltaTime;
                offset = Random.insideUnitSphere * _shakeStrength;
                if (_shakeTime <= 0) _shakeStrength = 0;
            }
            transform.position = basePosition + offset;
            transform.LookAt(lookAt + offset * 0.5f);
            if (_camera != null) _camera.fieldOfView = baseFieldOfView - _zoom * 10f;
        }
    }
}
