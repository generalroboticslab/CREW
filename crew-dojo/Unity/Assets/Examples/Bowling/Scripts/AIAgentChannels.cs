using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.SideChannels;
using Nakama.TinyJson;
using Dojo;

namespace Examples.Bowling
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
}
