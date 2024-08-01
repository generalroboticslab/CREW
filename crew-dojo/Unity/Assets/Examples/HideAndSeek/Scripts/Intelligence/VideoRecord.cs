using System.IO;
using UnityEngine;
using Dojo.Recording;
#if UNITY_STANDALONE || UNITY_EDITOR
using FFmpegOut;
#endif

namespace Examples.HideAndSeek
{
    public class VideoRecord : MonoBehaviour
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        [SerializeField] private PlayerController _controller;
        [SerializeField] private VideoCapture _captureEye;
        [SerializeField] private VideoCapture _captureMasked;
        [SerializeField] private VideoCapture _captureAccumu;
#endif

        private DojoRecord _record;

        private void Awake()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            _record = FindObjectOfType<DojoRecord>();
            ToggleCaptures(false);

            _controller.OnControllerReady += () =>
            {
                if (_record.IsRecording && _controller.IsOwner)
                {
                    SetupCaptures();
                    ToggleCaptures(true);
                }
            };
#endif
        }

        private void SetupCaptures()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            var uniqueID = _record.ClientIdentity;

            _captureEye.folderPath = Path.Combine("Recording", uniqueID, "Eye");
            _captureMasked.folderPath = Path.Combine("Recording", uniqueID, "Masked");
            _captureAccumu.folderPath = Path.Combine("Recording", uniqueID, "Accumulated");
#endif
        }

        private void ToggleCaptures(bool enable)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (_controller.EnableFirstCamera)
            {
                _captureEye.enabled = enable;
            }
            if (_controller.EnableMaskedCamera)
            {
                _captureMasked.enabled = enable;
            }
            if (_controller.EnableAccumuCamera)
            {
                _captureAccumu.enabled = enable;
            }
#endif
        }
    }
}
