using System.Text;
using Nakama;
using Nakama.TinyJson;

namespace Dojo
{
    /// <summary>
    /// A raw message in Dojo network, transferred in Nakama match
    /// </summary>
    public class DojoMessage
    {
        /** Raw data in byte array */
        public readonly byte[] RawData;

        /** Message type identifier */
        public readonly long OpCode;

        /** Sender presence on Nakama */
        public readonly IUserPresence Sender;

        /// <summary>
        /// Construct given Nakama.IMatchState recevied from Nakama match
        /// </summary>
        /// <param name="state">message data</param>
        public DojoMessage(IMatchState state)
        {
            RawData = state.State;
            OpCode = state.OpCode;
            Sender = state.UserPresence;
        }

        /// <summary>
        /// Construct a placeholder message for \p data
        /// </summary>
        /// <param name="data">raw message data</param>
        public DojoMessage(byte[] data)
        {
            RawData = data;
            OpCode = default;
            Sender = default;
        }

        /// <summary>
        /// Construct a placeholder message for \p data
        /// </summary>
        /// <param name="data">message content</param>
        public DojoMessage(string data)
        {
            RawData = Encoding.UTF8.GetBytes(data);
            OpCode = default;
            Sender = default;
        }

        /// <summary>
        /// Get message data as UTF8 string
        /// </summary>
        /// <returns>message content</returns>
        public string GetString()
        {
            return Encoding.UTF8.GetString(RawData);
        }

        public object GetUnderlyingMessage()
        {
            return JsonParser.FromJson<object>(GetString());
        }

        /// <summary>
        /// Decode message data into JSON-like data \p T
        /// </summary>
        /// <typeparam name="T">a JSON-like type</typeparam>
        /// <returns>decoded data</returns>
        public T GetDecodedData<T>()
        {
            return GetDecodedData<T>(GetString());
        }

        /// <summary>
        /// Decode message data into JSON-like data \p T
        /// </summary>
        /// <typeparam name="T">a JSON-like type</typeparam>
        /// <param name="data">encoded JSON message content</param>
        /// <returns>decoded data</returns>
        public static T GetDecodedData<T>(string data)
        {
            return JsonParser.FromJson<T>(data);
        }
    }
}
