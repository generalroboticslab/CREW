using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Dojo;

namespace Examples.Bowling
{
    public class GameBoard : MonoBehaviour
    {
        private const string LOGSCOPE = "GameBoard";

        [SerializeField]
        private InputActionAsset _playerActions;

        private InputActionMap _playerControl;

        [Tooltip("Send state update in every N seconds")]
        [SerializeField]
        private float _stateUpdateFreq = 0.1f;

        [SerializeField]
        private GameObject _prefabBall;

        [SerializeField]
        private GameObject _prefabPin;

        [SerializeField]
        private AIAgentManager _aiAgentManager;

        [HideInInspector] public DojoConnection Connection;
        private bool IsClient => Connection.IsClient;
        private bool IsPlayer => Connection.IsPlayer;
        private bool IsClientConnected = false;

        [HideInInspector] public event Action<NetCommand> OnNewAction;
        [HideInInspector] public event Action<byte[]> OnNewState;
        [HideInInspector] public event Action<int, int> OnFrameEnded;
        [HideInInspector] public event Action OnEpisodeEnded;

        private readonly Score _score = new();
        public GameStage _stage;

        private Transform _ball;
        private readonly List<Transform> _pins = new();
        private readonly List<Vector3> _pinsMoving = new();

        private Vector2 _shootDir = Vector2.zero;

        [SerializeField]
        private UIDocument _scoreUI;

        private Label _scoreLabel;
        private Label _frameLabel;
        private int last_score = 0;
        public int immediate_score = 0;
        public float[] vector_state = new float[32];
        public bool newReward = false;

        private int steer_counter = 0;
        private float steer_time = 0;
        private float steer_dir = 0.0f;


        private void Awake()
        {
            var root = _scoreUI.rootVisualElement;
            _scoreLabel = root.Q<Label>("Score");
            _frameLabel = root.Q<Label>("Frame");

            _playerControl = _playerActions.actionMaps[0];
            _playerControl.Enable();
            _stage = GameStage.Move;
            InitializeAll();
        }

        private void Update()
        {
            HandleClientControl();

            if (!IsClient)
            {
                var playerCount = Connection.MatchClients.Values.Where(role => role != DojoNetworkRole.Viewer).Count() + Connection.MatchAIPlayers.Count;
                if ((playerCount > 0 && !IsClientConnected) || (playerCount == 0 && IsClientConnected))
                {
                    ResetGameState();
                }
                IsClientConnected = playerCount > 0;

                var ballPos = _ball.position;
                vector_state[0] = ballPos.x;
                vector_state[1] = ballPos.y;
                var idx = 2;
                _pins.ForEach(p =>
                {
                    var pinPos = p.position;
                    vector_state[idx++] = (float)(p.gameObject.activeSelf ? 1 : 0);
                    vector_state[idx++] = pinPos.x;
                    vector_state[idx++] = pinPos.y;

                });


                NextState();
            }

            _frameLabel.text = $"Roll {_score.CurrentFrame + 1}";
            _scoreLabel.text = _score.sum_score.ToString();

        }

        #region Controls

        private void HandleClientControl()
        {
            if ((IsClient && IsPlayer) || !IsClient)
            {
                if (_playerControl["Up"].IsPressed())
                {
                    if (IsClient)
                    {
                        OnNewAction?.Invoke(NetCommand.Up);
                    }
                    else
                    {
                        HandleClientControl(NetCommand.Up);
                    }
                }
                if (_playerControl["Down"].IsPressed())
                {
                    if (IsClient)
                    {
                        OnNewAction?.Invoke(NetCommand.Down);
                    }
                    else
                    {
                        HandleClientControl(NetCommand.Down);
                    }
                }
                if (_playerControl["Roll"].WasPressedThisFrame())
                {
                    if (IsClient)
                    {
                        OnNewAction?.Invoke(NetCommand.Shoot);
                    }
                    else
                    {
                        HandleClientControl(NetCommand.Shoot);
                    }
                }
            }
        }

        public void HandleClientControl(NetCommand command)
        {
            if (IsClient && IsPlayer)
            {
                OnNewAction?.Invoke(command);
            }
            else if (!IsClient && _stage != GameStage.Collect)
            {
                switch (command)
                {
                    case NetCommand.Up:
                        {
                            if (_stage == GameStage.Move)
                            {
                                var pos = _ball.position;
                                pos.y = Math.Clamp(pos.y + 0.05f, -2.5f, 2.5f);
                                _ball.position = pos;
                            }
                            else if (_stage == GameStage.Shoot && _shootDir.y == 0.0f)
                            {
                                _shootDir.y = 0.4f;
                            }
                        }
                        break;

                    case NetCommand.Down:
                        {
                            if (_stage == GameStage.Move)
                            {
                                var pos = _ball.position;
                                pos.y = Math.Clamp(pos.y - 0.05f, -2.5f, 2.5f);
                                _ball.position = pos;
                            }
                            else if (_stage == GameStage.Shoot && _shootDir.y == 0.0f)
                            {
                                _shootDir.y = -0.4f;
                            }
                        }
                        break;

                    case NetCommand.Shoot:
                        {
                            if (_stage == GameStage.Move)
                            {
                                _shootDir = new Vector2(2.5f, 0.0f);
                                _stage = GameStage.Shoot;
                            }
                        }
                        break;
                }
            }
        }

        public void ExecuteActionVector(Vector3 action)
        {
            var move_to = Math.Clamp(action[0] * 2.5f, -2.5f, 2.5f);
            steer_time = Math.Clamp(action[1] * 30 + 30, 0.0f, 60.0f); // 2 seconds
            steer_dir = Math.Sign(action[2]) * 0.4f; //

            
            
            Debug.Log($"{_stage}, {move_to}, {steer_time}, {steer_dir}");

            if (IsClient)
            {
                return;
            }
            else if (_stage == GameStage.Move)
            {
                var pos = _ball.position;
                pos.y = Math.Clamp(move_to, -2.5f, 2.5f); 
                _ball.position = pos;
                _shootDir = new Vector2(2.5f, 0.0f);
                _stage = GameStage.Shoot;
                steer_counter = 0;
            }
        }
        #endregion Controls

        #region State Update

        public void HandleEvents(List<string> data)
        {
            var name = data[0];
            if (name.Equals("FrameEnded"))
            {
                if (data.Count >= 3 && int.TryParse(data[1], out var frameCount) && int.TryParse(data[2], out var score))
                {
                    OnFrameEnded?.Invoke(frameCount, score);
                }
            }
            else
            {
                Debug.LogWarning($"{LOGSCOPE}: Invalid event {name}");
            }
        }

        private void NextTick()
        {
            if (!IsClient && IsClientConnected && _ball != null)
            {
                var state = EncodeState();
                OnNewState?.Invoke(state);
            }
        }

        private void NextState()
        {
            if (!IsClient)
            {
                if (_stage == GameStage.Shoot)
                {
                    if (_shootDir.y == 0.0f) // steer
                    {
                        if (steer_counter > steer_time) 
                        {
                            _shootDir.y = steer_dir ;
                        }
                        else
                        {
                            steer_counter += 1;
                        }
                    }
                    var pos = _ball.position;
                    var t = _shootDir * 0.05f;
                    pos.x = Math.Clamp(pos.x + t.x, -8.5f, 8.5f);
                    pos.y = Math.Clamp(pos.y + t.y, -2.5f, 2.5f);
                    _ball.position = pos;

                    var ballB = _ball.GetComponent<SpriteRenderer>().bounds;

                    if (pos.x >= 8.5f)
                    {
                        _stage = GameStage.Collect;
                    }

                    for (var i = 0; i < _pins.Count; ++i)
                    {
                        var p = _pins[i];
                        var m = _pinsMoving[i];
                        if (p.gameObject.activeSelf)
                        {
                            if (!m.Equals(Vector3.zero))
                            {
                                pos = p.position;
                                pos.x += m.x;
                                pos.y += m.y;
                                m.z += 1.0f;
                                _pinsMoving[i] = m;
                                p.position = pos;

                                if (pos.x > 7.5f || pos.y > 2.0f || pos.y < -2.0f || m.z > 4.0f)
                                {
                                    p.gameObject.SetActive(false);
                                    _pinsMoving[i] = Vector3.zero;
                                }
                            }
                            else
                            {
                                var toTest = new List<Bounds>() { ballB };
                                for (var j = 0; j < _pins.Count; ++j)
                                {
                                    if (i != j && !_pinsMoving[j].Equals(Vector3.zero))
                                    {
                                        toTest.Add(_pins[j].GetComponent<SpriteRenderer>().bounds);
                                    }
                                }

                                var pinB = p.GetComponent<SpriteRenderer>().bounds;

                                for (var bIdx = 0; bIdx < toTest.Count; ++bIdx)
                                {
                                    var b = toTest[bIdx];
                                    if (pinB.Intersects(b))
                                    {
                                        if (bIdx == 0)
                                        {
                                            pos = _ball.position;
                                            if (pinB.center.y < ballB.center.y)
                                            {
                                                pos.y = Math.Clamp(pos.y + 0.25f, -2.5f, 2.5f);
                                            }
                                            else if (pinB.center.y > ballB.center.y)
                                            {
                                                pos.y = Math.Clamp(pos.y - 0.25f, -2.5f, 2.5f);
                                            }
                                            _ball.position = pos;
                                        }

                                        _pinsMoving[i] = new Vector3(Math.Max(pinB.center.x - b.center.x, 0.0f), pinB.center.y - b.center.y, 0.0f);
                                        _pinsMoving[i] = _pinsMoving[i].normalized * 0.1f;

                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (_stage == GameStage.Collect)
                {
                    var pos = _ball.position;

                    if (true)
                    {
                        pos.x = -6.5f;
                        pos.y = -1.5f;
                        _ball.position = pos;
 
                        _stage = GameStage.Move;
                        var clearedPins = _pins.Count(p => !p.gameObject.activeSelf);

                        newReward = true;
                        immediate_score = clearedPins - _score._prevClearedPins;
                        _score.TakeNewRoll(clearedPins);

                        if (_score.CheckFrameOver() || clearedPins >= Score.MaxScore)
                        {
                            OnFrameEnded?.Invoke(_score.CurrentFrame, _score.TotalScore - last_score);
                            last_score = _score.TotalScore;
                            InitializeAll();
                        }
                        if (_score.CheckEpisodeOver())
                        {
                            OnEpisodeEnded?.Invoke();
                            ResetGameState();
                            last_score = 0;
                        }
                    }
                }
            }
        }

        public async void ResetGameState()
        {
            CancelInvoke(nameof(NextTick));

            _score.Reset();
            _stage = GameStage.Move;
            InitializeAll();
            NextTick();

            await Task.Delay(500);

            if (!IsClient)
            {
                InvokeRepeating(nameof(NextTick), _stateUpdateFreq, _stateUpdateFreq);
            }
        }

        #endregion State Update

        #region Game State Encoding

        public byte[] EncodeState()
        {
            Debug.Assert(!IsClient);
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            _score.Encode(writer);
            writer.Write((byte)_stage);

            var ballPos = _ball.position;
            writer.Write(ballPos.x);
            writer.Write(ballPos.y);


            _pins.ForEach(p =>
            {
                var pinPos = p.position;
                writer.Write(p.gameObject.activeSelf);

                writer.Write(pinPos.x);
                writer.Write(pinPos.y);
            });

            

            return stream.ToArray();
        }

        public void DecodeState(byte[] data)
        {
            Debug.Assert(IsClient);
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            _score.Decode(reader);
            _stage = (GameStage)reader.ReadByte();

            _ball.position = new Vector3(
                reader.ReadSingle(),
                reader.ReadSingle(),
                -0.1f
            );

            _pins.ForEach(p =>
            {
                p.gameObject.SetActive(reader.ReadBoolean());
                p.position = new Vector3(
                   reader.ReadSingle(),
                   reader.ReadSingle(),
                   -0.1f
                );
            });
        }

        #endregion Game State Encoding

        #region Object Management

        private void InitializeAll()
        {
            if (_ball == null)
            {
                _ball = Instantiate(_prefabBall, transform).transform;
            }
            if (_pins.Count == 0)
            {
                for (var i = 0; i < 10; ++i)
                {
                    _pins.Add(Instantiate(_prefabPin, transform).transform);
                    _pinsMoving.Add(Vector3.zero);
                }
            }
            _pins.ForEach(p => p.gameObject.SetActive(true));
            _ball.position = new Vector3(-6.5f, -1.5f, -0.1f);

            _pinsMoving.ForEach(p =>
            {
                p.x = 0.0f;
                p.y = 0.0f;
                p.z = 0.0f;
            });

            var rowCount = 0;
            var idx = 0;
            while (idx < 10)
            {
                for (var i = 0; i < rowCount + 1; ++i)
                {
                    _pins[idx + i].position = new Vector3(
                        5.5f + 0.7f * rowCount, 0.8f * i - 0.4f * rowCount, -0.1f
                    );
                }
                rowCount++;
                idx += rowCount;
            }
        }

        #endregion Object Management
    }
}
