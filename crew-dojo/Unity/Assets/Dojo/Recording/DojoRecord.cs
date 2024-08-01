using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Nakama;
using Nakama.TinyJson;
using Dojo.Nakama;

namespace Dojo.Recording
{
    /// <summary>
    /// Class for recording and data collection
    /// </summary>
    public class DojoRecord : MonoBehaviour
    {
        private const string LOGSCOPE = "DojoRecord";

        [SerializeField]
        private string _logFileName = "recording.log";

        private DojoConnection _connection;

        private bool _isRecordingClient = false;

        private string _clientIdentity = "DojoRecord";

        /** Unique client identity used in video recording */
        public string ClientIdentity => _clientIdentity;

        private readonly HashSet<IUserPresence> _recordingClients = new();

        /** Is client currently recording? */
        public bool IsRecording => _recordingClients.Count > 0;

        private FileStream _logFile;
        private StreamWriter _logFileWriter;

        /** Invoked when recording is first enabled */
        public event Action OnEnableRecording;

        /** Invoked when recording is disabled */
        public event Action OnDisableRecording;

        private void Awake()
        {
            _connection = FindObjectOfType<DojoConnection>();
            _connection.OnJoinedMatch += OnJoinedMatch;
            _connection.OnLeftMatch += OnLeftMatch;
            _connection.OnMatchPlayerJoined += m => OnMatchPlayerJoined();

            _connection.SubscribeRemoteMessages((long)NakamaOpCode.RecordStart, OnRemoteRecordStart, false);
            _connection.SubscribeRemoteMessages((long)NakamaOpCode.RecordStop, OnRemoteRecordStop, false);
            _connection.SubscribeRemoteMessages((long)NakamaOpCode.RecordQuery, OnRemoteRecordQuery, false);

#if UNITY_STANDALONE // && !UNITY_EDITOR
            // parse commands
            if (_connection.IsViewer)
            {
                var args = Environment.GetCommandLineArgs();
                for (var idx = 0; idx < args.Length; ++idx)
                {
                    var arg = args[idx];
                    if (arg.Equals("-DojoRecording"))
                    {
                        _isRecordingClient = true;
                    }
                    else if (arg.Equals("-DojoRecordingFile") && idx < args.Length - 1)
                    {
                        // overwrite log file name
                        _logFileName = args[idx + 1];
                        ++idx;
                    }
                    else if (arg.Equals("-DojoRecordingIdentity") && idx < args.Length - 1)
                    {
                        _clientIdentity = args[idx + 1];
                        ++idx;
                    }
                }
            }
#endif

            if (_isRecordingClient)
            {
                _connection.SubscribeRemoteMessages((long)NakamaOpCode.RecordEvent, OnRemoteRecordEvent, false);

                // open filestream
                _logFile = new(_logFileName, FileMode.Append);
                _logFileWriter = new(_logFile);
                var timestamp = DateTime.Now.ToString(@"MM\/dd\/yyyy h\:mm tt");
                WriteToLog($"//<! Recording started at {timestamp}");
            }
        }

        private void OnDestroy()
        {
            if (_isRecordingClient)
            {
                var timestamp = DateTime.Now.ToString(@"MM\/dd\/yyyy h\:mm tt");
                WriteToLog($"//>! Recording stopped at {timestamp}");
                _logFileWriter.Close();
                _logFile.Close();
                OnLeftMatch();
            }
        }

        private async void OnJoinedMatch()
        {
            if (_isRecordingClient)
            {
                // notify all
                OnMatchPlayerJoined();
                _recordingClients.Add(_connection.MatchSelf);
                OnEnableRecording?.Invoke();
            }
            else if (_connection.IsClient)
            {
                await _connection.SendStateMessage((long)NakamaOpCode.RecordQuery, "Anyone recording?", broadcast: true);
            }
        }

        private void OnLeftMatch()
        {
            if (_isRecordingClient)
            {
                _connection.SendStateMessage((long)NakamaOpCode.RecordStop, "Not Recording!", broadcast: true);
                OnDisableRecording?.Invoke();
            }
            _recordingClients.Clear();
        }

        private async void OnMatchPlayerJoined()
        {
            if (_isRecordingClient)
            {
                // notify all
                await _connection.SendStateMessage((long)NakamaOpCode.RecordStart, "Recording!", broadcast: true);
            }
        }

        private void OnRemoteRecordQuery(DojoMessage m)
        {
            if (_isRecordingClient)
            {
                OnMatchPlayerJoined();
            }
        }

        private void OnRemoteRecordStart(DojoMessage m)
        {
            _recordingClients.Add(m.Sender);
            if (_recordingClients.Count == 1)
            {
                OnEnableRecording?.Invoke();
            }
        }

        private void OnRemoteRecordStop(DojoMessage m)
        {
            if (_recordingClients.Count == 1)
            {
                OnDisableRecording?.Invoke();
            }
            _recordingClients.Remove(m.Sender);
        }

        private void OnRemoteRecordEvent(DojoMessage m)
        {
            if (_isRecordingClient)
            {
                var decoded = m.GetDecodedData<List<string>>();
                if (decoded.Count == 5)
                {
                    // LOG FORMAT:
                    // !!timestamp
                    // userID userRole eventType
                    // eventData
                    WriteToLog($"!!{decoded[0]}\n{decoded[1]} {decoded[2]} {decoded[3]}\n{decoded[4]}\n");
                }
                else
                {
                    Debug.LogWarning($"{LOGSCOPE}: Invalid record event received!");
                }
            }
        }

        /// <summary>
        /// Dispatch event data to the recording client
        /// </summary>
        /// <param name="eventType">event type identifier</param>
        /// <param name="eventData">encoded event data</param>
        /// <returns>\p Task to be awaited</returns>
        public Task DispatchEvent(string eventType, string eventData)
        {
            return DispatchEvent(eventType, eventData, _connection.MatchSelf, _connection.Role);
        }

        /// <summary>
        /// Dispatch event data to the recording client from \p user with \p role
        /// </summary>
        /// <param name="eventType">event type identifier</param>
        /// <param name="eventData">encoded event data</param>
        /// <param name="user">user presence on Nakama</param>
        /// <param name="role">user current role in %Dojo</param>
        /// <returns>\p Task to be awaited</returns>
        public Task DispatchEvent(string eventType, string eventData, IUserPresence user, DojoNetworkRole role)
        {
            if (IsRecording)
            {
                var now = DateTime.UtcNow.Ticks;
                var toEncode = new List<string>() { now.ToString(), user.UserId, role.ToString(), eventType, eventData };
                return _connection.SendStateMessage((long)NakamaOpCode.RecordEvent, JsonWriter.ToJson(toEncode), targets: _recordingClients);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        private void WriteToLog(string message, bool lineBreak = true)
        {
            if (_isRecordingClient)
            {
                if (lineBreak)
                {
                    _logFileWriter.WriteLine(message);
                }
                else
                {
                    _logFileWriter.Write(message);
                }
            }
        }
    }
}
