using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.SideChannels;
using Nakama.TinyJson;
using Dojo;

namespace Examples.HideAndSeek
{
    public class EventChannel : SideChannel
    {
        private const string LOGSCOPE = "EventChannel";

        private readonly DojoConnection _connection;

        public readonly bool IsInitialized;

        public EventChannel(DojoConnection connection)
        {
            IsInitialized = false;
            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];
                if (arg.Equals("-EventChannelID") && idx < args.Length - 1)
                {
                    ChannelId = new Guid(args[idx + 1]);
                    Debug.Log($"ChannelID: {ChannelId}");
                    IsInitialized = true;
                    break;
                }
            }

            _connection = connection;
            if (IsInitialized)
            {
                GameManager.Instance.OnGameStart += OnGameEpisodeStarted;
                GameManager.Instance.OnGameStop += OnGameEpisodeStopped;
                GameManager.Instance.OnSeekerCaught += OnSeekerCaught;
            }
        }

        protected override void OnMessageReceived(IncomingMessage msg)
        {

        }

        private void OnGameEpisodeStarted(DojoMessage m)
        {
            // "E" for Event
            var eventData = new List<object>() { "E", "EpisodeStart", m.GetUnderlyingMessage() };

            // send feedback
            using (var msgOut = new OutgoingMessage())
            {
                msgOut.WriteString(JsonWriter.ToJson(eventData));
                QueueMessageToSend(msgOut);
            }

            Debug.Log($"{LOGSCOPE}: OnGameEpisodeStarted");
        }

        private void OnGameEpisodeStopped(DojoMessage m)
        {
            // "E" for Event
            var eventData = new List<object>() { "E", "EpisodeStop", m.GetUnderlyingMessage() };

            // send feedback
            using (var msgOut = new OutgoingMessage())
            {
                msgOut.WriteString(JsonWriter.ToJson(eventData));
                QueueMessageToSend(msgOut);
            }

            Debug.Log($"{LOGSCOPE}: OnGameEpisodeStopped");
        }

        private void OnSeekerCaught(DojoMessage m)
        {
            // "E" for Event
            var eventData = new List<object>() { "E", "SeekerHasCaught", m.GetUnderlyingMessage() };

            // send feedback
            using (var msgOut = new OutgoingMessage())
            {
                msgOut.WriteString(JsonWriter.ToJson(eventData));
                QueueMessageToSend(msgOut);
            }

            Debug.Log($"{LOGSCOPE}: OnSeekerCaught");
        }
    }

    /// <summary>
    /// Construct a communication channel and handles time toggling
    /// </summary>
    public class ToggleTimestepChannel : SideChannel
    {
        private const string LOGSCOPE = "ToggleTimestepChannel";

        private readonly GameManager _gameManager;

        public readonly bool IsInitialized;

        public ToggleTimestepChannel(GameManager gameManager)
        {
            IsInitialized = false;
            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];
                if (arg.Equals("-ToggleTimestepChannelID") && idx < args.Length - 1)
                {
                    ChannelId = new Guid(args[idx + 1]);
                    Debug.Log($"ChannelID: {ChannelId}");
                    IsInitialized = true;
                    break;
                }
            }

            _gameManager = gameManager;
        }

        protected override void OnMessageReceived(IncomingMessage msg)
        {
            if (IsInitialized)
            {
                _gameManager.PauseGame();
                Debug.Log($"{LOGSCOPE}: OnMessageReceived");
            }
        }
    }
}
