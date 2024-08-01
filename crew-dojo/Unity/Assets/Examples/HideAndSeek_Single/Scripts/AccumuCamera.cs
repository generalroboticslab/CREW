using System.Collections.Generic;
using UnityEngine;

namespace Examples.HideAndSeek_Single
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
        // private List<RenderTexture> _accTexScreen = new();
        private int _accTexScreenIdx = 0;

        // for AI capture
        private readonly List<RenderTexture> _accTexAgent = new();
        // private List<RenderTexture> _accTexAgent = new();
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
                
                var isAgent = _fullCam.targetTexture != null; // if target texture is null, it's screen camera
                var accTex = isAgent ? _accTexAgent : _accTexScreen; // select texture set, if it's agent, use agent texture set
                var texIdx = isAgent ? _accTexAgentIdx : _accTexScreenIdx; // select texture index. if it's agent, use agent texture index
                var texIdxNext = (texIdx + 1) % 2; // select next texture index

                CheckAccumulateTexture(source.width, source.height, isAgent); // check if texture set is valid

                var envRT = RenderTexture.GetTemporary(source.width, source.height, 16, RenderTextureFormat.ARGB32); // create temporary render texture for environment

                var prevEnvRT = _envCamera.targetTexture; // save previous environment render texture
                // Debug.Log($"{prevEnvRT}");
                _envCamera.targetTexture = envRT; // set environment render texture to temporary render texture
                _envCamera.Render(); // render environment into temporary render texture
                _envCamera.targetTexture = prevEnvRT; // restore previous environment render texture

                // render environment into accumulate

                var cameraPoint = _envCamera.WorldToScreenPoint(_playerBody.transform.position); // get player position in screen space
                var scaledPoint = new Vector2(
                    cameraPoint.x / Screen.width,
                    cameraPoint.y / Screen.height
                );

                _blitMaterial.SetTexture("_EnvTex", envRT);
                _blitMaterial.SetTexture("_AccTex", accTex[texIdxNext]);
                _blitMaterial.SetFloat("_MaskRatio", _maskRatio * 0.5f);
                _blitMaterial.SetFloat("_ScreenRatio", source.width / (float)source.height);
                _blitMaterial.SetVector("_PlayerPosition", scaledPoint);

                Graphics.Blit(null, accTex[texIdx], _blitMaterial); // 
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

        private void CheckAccumulateTexture(int width, int height, bool isAgent) // check if texture set is valid
        {
            var accTex = isAgent ? _accTexAgent : _accTexScreen; // select texture set, if it's agent, use agent texture set
            if (accTex.Count != 2 || // if texture set count is not 2
                accTex[0].width != width || // or width is not same
                accTex[0].height != height) // or height is not same
            {

                // Debug.Log($"Accumulate texture set is invalid. Creating new set. width: {width}, height: {height}");
                accTex.ForEach(t => Destroy(t)); 
                accTex.Clear();
                accTex.Add(new(width, height, 16, RenderTextureFormat.ARGB32));
                accTex.Add(new(width, height, 16, RenderTextureFormat.ARGB32));
            }
        }

        public void ClearAccumulation()
        {
            _accTexScreen.ForEach(t => Destroy(t));
            _accTexAgent.ForEach(t => Destroy(t));
            _accTexAgent.Clear();
            _accTexScreen.Clear();
            _accTexScreenIdx = 0;
            _accTexAgentIdx = 0;
            _envCamera.targetTexture = null;
        }
    }
}
