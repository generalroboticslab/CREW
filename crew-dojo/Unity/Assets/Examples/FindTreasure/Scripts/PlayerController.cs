using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Netcode;
using Dojo;
using Dojo.Netcode;

namespace Examples.FindTreasure
{
    public class PlayerController : NetworkBehaviour
    {
        private const string LOGSCOPE = "PlayerController";

        [SerializeField]
        private float _moveSpeed = 2f;

        [SerializeField, Tooltip("Rotation speed (in degrees)")]
        private float _rotateSpeed = 50f;

        [SerializeField]
        private UIDocument inGameUI;

        public Rigidbody _body;
        public Vector3 _offset;
        public Vector3 _angleOffset;

        private Camera _globalCamera;

        public event Action OnControllerReady;

        private DojoConnection _connection;
        private DojoTransport _transport;

        [SerializeField]
        private Camera _eyeCam;

        [SerializeField]
        private AccumuCamera _accCam;

        [SerializeField]
        private AccumuCamera _accCamSens;

        public AccumuCamera CamAcc => _accCam;
        public AccumuCamera CamAccSens => _accCamSens;

        [SerializeField]
        private Camera _egoCam;

        [SerializeField]
        private Camera _egoCamSens;

        [SerializeField]
        private InputActionAsset _playerActions;

        private InputActionMap _playerControl;

        [HideInInspector]
        public NetworkVariable<Vector2> humanAction = new NetworkVariable<Vector2>();

        private HumanInterface _humanInterface;

        private Vector3 last_pos;

        public NetworkVariable<bool> clear_cam_flag = new NetworkVariable<bool>(false);
        public NetworkVariable<int> cnt = new NetworkVariable<int>(0);

        public Unity.Netcode.Components.NetworkTransform networkTransform;

        public NetworkVariable<float> serverscreenwidth = new NetworkVariable<float>(0f);
        public NetworkVariable<float> serverscreenheight = new NetworkVariable<float>(0f);

        private void Awake()
        {

            _body = GetComponentInChildren<Rigidbody>();
            networkTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            _offset = Vector3.zero;
            _angleOffset = Vector3.zero;

            _globalCamera = Camera.main;

            var uiRoot = inGameUI.rootVisualElement;
            inGameUI.rootVisualElement.style.display = DisplayStyle.None;

            _playerControl = _playerActions.actionMaps[0];
            _playerControl.Enable();

            _connection = FindObjectOfType<DojoConnection>();
            _humanInterface = FindObjectOfType<HumanInterface>();

            last_pos = _body.position;

        }

        private void Update()
        {

            last_pos = _body.position;

            SwitchCameraIfNeeded();
            // UpdateHumanAction();

            if (clear_cam_flag.Value && IsClient)
            {
                _accCam.ClearAccumulation();
                _accCamSens.ClearAccumulation();
                ChangeFlagServerRpc();
            }
            if (IsServer)
            {
                serverscreenwidth.Value = Screen.width;
                serverscreenheight.Value = Screen.height;
            }

            if (_humanInterface._isControllingAgent)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Vector3 p3 = Mouse.current.position.ReadValue();
                    MoveToCLickedPosition(p3, true);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ChangeFlagServerRpc()
        {
            clear_cam_flag.Value = false;
        }

        private void FixedUpdate()
        {

        }

        public void MoveToCLickedPosition(Vector3 p3, bool IshumanControl)
        {
            if (IsServer)
            {
                if (IshumanControl)
                {
                    Ray ray = _globalCamera.ScreenPointToRay(p3);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit))
                    {

                        transform.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = true;
                        transform.GetComponent<UnityEngine.AI.NavMeshAgent>().ResetPath();
                        transform.GetComponent<UnityEngine.AI.NavMeshAgent>().SetDestination(hit.point);
                    }
                }
            }
            else
            {
                p3 = new Vector3(p3.x / Screen.width * serverscreenwidth.Value, p3.y / Screen.height * serverscreenheight.Value, 0f);
                MoveToCLickedPositionServerRpc(p3, IshumanControl);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner && IsClient)
            {
                OnGainedOwnership();
            }
            OnControllerReady?.Invoke();

            _transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as DojoTransport;
            if (true)
            {
                _accCam.IsEnabled = true;
                _accCamSens.IsEnabled = true;

                _accCam.FollowGlobalCamera(_globalCamera);
                _accCamSens.FollowGlobalCamera(_globalCamera);
            }
        }


        public override void OnNetworkDespawn()
        {
            if (IsOwner && IsClient)
            {
                OnLostOwnership();
            }
        }

        public override void OnGainedOwnership()
        {
            if (IsClient)
            {
                Debug.Log($"{LOGSCOPE}: Gained Ownership");
                inGameUI.rootVisualElement.style.display = DisplayStyle.Flex;
            }
        }

        public override void OnLostOwnership()
        {
            if (IsClient)
            {
                Debug.Log($"{LOGSCOPE}: Lost Ownership");
            }
        }

        public void SetRotationSpeed(float speed)
        {
            if (IsServer)
            {
                _rotateSpeed = speed;
            }
            else
            {
                SetRotationSpeedServerRpc(speed);
            }
        }

        public void SetMoveSpeed(float speed)
        {
            if (IsServer)
            {
                _moveSpeed = speed;
            }
            else
            {
                SetMoveSpeedServerRpc(speed);
            }
        }

        private void SwitchCameraIfNeeded()
        {
            if (!_connection.IsServer && !_humanInterface.IsWrittenFeedbackVisible)
            {
                if (_playerControl["Camera1"].IsPressed())
                {
                    _eyeCam.depth = -10;
                    _globalCamera.depth = 1;
                    _accCam.FullCamera.depth = -10;
                    _egoCam.depth = -10;
                }
                else if (_playerControl["Camera2"].IsPressed())
                {
                    _eyeCam.depth = 1;
                    _globalCamera.depth = -10;
                    _accCam.FullCamera.depth = -10;
                    _egoCam.depth = -10;

                }
                else if (_playerControl["Camera3"].IsPressed())
                {
                    _eyeCam.depth = -10;
                    _globalCamera.depth = -10;
                    _accCam.FullCamera.depth = 1;
                    _egoCam.depth = -10;
                }
                else if (_playerControl["Camera4"].IsPressed())
                {
                    _eyeCam.depth = -10;
                    _globalCamera.depth = -10;
                    _accCam.FullCamera.depth = -10;
                    _egoCam.depth = 1;
                }

                _accCamSens.FullCamera.depth = _accCam.FullCamera.depth;
                _egoCamSens.depth = _egoCam.depth;
            }
        }

        private void UpdateHumanAction(Vector2 a)
        {
            if (_connection.IsClient)
            {
                UpdateHumanActionServerRpc(a);
            }
            else
            {
                humanAction.Value = a;
            }
        }

        [ServerRpc]
        private void SetRotationSpeedServerRpc(float speed)
        {
            SetRotationSpeed(speed);
        }

        [ServerRpc]
        private void SetMoveSpeedServerRpc(float speed)
        {
            SetMoveSpeed(speed);
        }

        [ServerRpc(RequireOwnership = false)]
        private void UpdateHumanActionServerRpc(Vector2 action)
        {
            humanAction.Value = action;
        }

        [ServerRpc(RequireOwnership = false)]
        private void MoveToCLickedPositionServerRpc(Vector3 p3, bool IshumanControl)
        {
            if (IshumanControl)
            {
                Ray ray = _globalCamera.ScreenPointToRay(p3);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    var navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                    navMeshAgent.enabled = true;
                    navMeshAgent.ResetPath();
                    navMeshAgent.SetDestination(hit.point);
                    UpdateHumanAction(new Vector2(hit.point.x, hit.point.z));
                }
            }
        }

        public void Teleport(Vector3 position)
        {
            if (IsServer)
            {
                transform.GetComponent<UnityEngine.AI.NavMeshAgent>().ResetPath();
                networkTransform.Teleport(position, transform.rotation, transform.localScale);
            }
        }
    }
}
