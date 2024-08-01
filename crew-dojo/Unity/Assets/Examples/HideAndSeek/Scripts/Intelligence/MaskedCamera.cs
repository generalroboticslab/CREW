using UnityEngine;

namespace Examples.HideAndSeek
{
    [RequireComponent(typeof(Camera))]
    public class MaskedCamera : MonoBehaviour
    {
        [SerializeField]
        private Transform _playerBody;

        [SerializeField, Range(0.0f, 1.0f)]
        private float _maskRatio = 0.2f;

        [SerializeField]
        private Shader _maskShader;

        private bool _isEnabled = false;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = _envCamera.enabled = value; }
        }

        private Material _blitMaterial;
        private Camera _envCamera;

        public Camera EnvCamera => _envCamera;

        private void Awake()
        {
            _blitMaterial = new Material(_maskShader);

            _envCamera = GetComponent<Camera>();
            IsEnabled = false;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (IsEnabled)
            {
                var cameraPoint = _envCamera.WorldToScreenPoint(_playerBody.transform.position);
                var scaledPoint = new Vector2(
                    cameraPoint.x / source.width,
                    cameraPoint.y / source.height
                );

                _blitMaterial.SetTexture("_EnvTex", source);
                _blitMaterial.SetFloat("_MaskRatio", _maskRatio * 0.5f);
                _blitMaterial.SetFloat("_ScreenRatio", source.width / (float)source.height);
                _blitMaterial.SetVector("_PlayerPosition", scaledPoint);

                Graphics.Blit(source, destination, _blitMaterial);
            }
        }

        public void FollowGlobalCamera(Camera globalCam)
        {
            transform.SetPositionAndRotation(globalCam.transform.position, globalCam.transform.rotation);
        }
    }
}
