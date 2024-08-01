using System.Collections.Generic;
using UnityEngine;

namespace Examples.HideAndSeek
{
    [RequireComponent(typeof(Camera))]
    public class AccumuCamera : MonoBehaviour
    {
        [SerializeField]
        private Transform _playerBody;

        [SerializeField]
        private Camera _envCamera;

        [SerializeField, Range(0.0f, 1.0f)]
        private float _maskRatio = 0.2f;

        [SerializeField]
        private Shader _accShader;

        private bool _isEnabled = false;

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = _fullCam.enabled = value; }
        }

        private Material _blitMaterial;
        private Camera _fullCam;

        // two set of render textures
        // for screen:
        private readonly List<RenderTexture> _accTexScreen = new();
        private int _accTexScreenIdx = 0;

        // for AI capture
        private readonly List<RenderTexture> _accTexAgent = new();
        private int _accTexAgentIdx = 0;

        public Camera FullCamera => _fullCam;

        private void Awake()
        {
            _blitMaterial = new Material(_accShader);

            _fullCam = GetComponent<Camera>();
            IsEnabled = false;
        }

        private void OnDestroy()
        {
            _accTexScreen.ForEach(t => Destroy(t));
            _accTexAgent.ForEach(t => Destroy(t));
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (IsEnabled)
            {
                var isAgent = _fullCam.targetTexture != null;
                var accTex = isAgent ? _accTexAgent : _accTexScreen;
                var texIdx = isAgent ? _accTexAgentIdx : _accTexScreenIdx;
                var texIdxNext = (texIdx + 1) % 2;

                CheckAccumulateTexture(source.width, source.height, isAgent);

                var envRT = RenderTexture.GetTemporary(source.width, source.height, 16, RenderTextureFormat.ARGB32);

                var prevEnvRT = _envCamera.targetTexture;
                _envCamera.targetTexture = envRT;
                _envCamera.Render();
                _envCamera.targetTexture = prevEnvRT;

                // render environment into accumulate

                var cameraPoint = _envCamera.WorldToScreenPoint(_playerBody.transform.position);
                var scaledPoint = new Vector2(
                    cameraPoint.x / Screen.width,
                    cameraPoint.y / Screen.height
                );

                _blitMaterial.SetTexture("_EnvTex", envRT);
                _blitMaterial.SetTexture("_AccTex", accTex[texIdxNext]);
                _blitMaterial.SetFloat("_MaskRatio", _maskRatio * 0.5f);
                _blitMaterial.SetFloat("_ScreenRatio", source.width / (float)source.height);
                _blitMaterial.SetVector("_PlayerPosition", scaledPoint);

                Graphics.Blit(null, accTex[texIdx], _blitMaterial);
                //Graphics.Blit(accTex[texIdx], destination);

                // render real camera into destination
                _blitMaterial.SetTexture("_EnvTex", source);
                _blitMaterial.SetTexture("_AccTex", accTex[texIdx]);
                _blitMaterial.SetFloat("_MaskRatio", _maskRatio * 0.5f);
                _blitMaterial.SetFloat("_ScreenRatio", source.width / (float)source.height);
                _blitMaterial.SetVector("_PlayerPosition", scaledPoint);
                Graphics.Blit(null, destination, _blitMaterial);

                if (isAgent)
                {
                    _accTexAgentIdx = texIdxNext;
                }
                else
                {
                    _accTexScreenIdx = texIdxNext;
                }

                RenderTexture.ReleaseTemporary(envRT);
            }
        }

        public void FollowGlobalCamera(Camera globalCam)
        {
            transform.SetPositionAndRotation(globalCam.transform.position, globalCam.transform.rotation);
        }

        private void CheckAccumulateTexture(int width, int height, bool isAgent)
        {
            var accTex = isAgent ? _accTexAgent : _accTexScreen;
            if (accTex.Count != 2 ||
                accTex[0].width != width ||
                accTex[0].height != height)
            {
                accTex.ForEach(t => Destroy(t));
                accTex.Clear();
                accTex.Add(new(width, height, 16, RenderTextureFormat.ARGB32));
                accTex.Add(new(width, height, 16, RenderTextureFormat.ARGB32));
            }
        }
    }
}
