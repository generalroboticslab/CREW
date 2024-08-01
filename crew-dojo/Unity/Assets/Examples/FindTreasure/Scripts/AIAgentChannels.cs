using System;
using UnityEngine;
using Unity.MLAgents.SideChannels;
using Dojo;
using System.Collections.Generic;
using Nakama.TinyJson;

namespace Examples.FindTreasure
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
        }

        protected override void OnMessageReceived(IncomingMessage msg)
        {

        }
    }

    public class WrittenFeedbackChannel : SideChannel
    {
        private const string LOGSCOPE = "WrittenFeedbackChannel";

        private readonly DojoConnection _connection;

        public readonly bool IsInitialized;

        public WrittenFeedbackChannel(DojoConnection connection)
        {
            IsInitialized = false;
            var args = Environment.GetCommandLineArgs();

            for (var idx = 0; idx < args.Length; ++idx)
            {
                var arg = args[idx];
                if (arg.Equals("-WrittenFeedbackChannelID") && idx < args.Length - 1)
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
                _connection.SubscribeRemoteMessages((long)NetOpCode.ReceiveWrittenFeedback, SendWrittenFeedback);
            }
        }

        protected override void OnMessageReceived(IncomingMessage msg)
        {

        }

        public void SendWrittenFeedback(DojoMessage m)
        {
            // "WF" for Written Feedback
            var eventData = new List<object>() { "WF", m.GetUnderlyingMessage() };

            // send feedback
            using (var msgOut = new OutgoingMessage())
            {
                msgOut.WriteString(JsonWriter.ToJson(eventData));
                QueueMessageToSend(msgOut);
            }

            Debug.Log($"{LOGSCOPE}: SendWrittenFeedback");
        }
    }
}
