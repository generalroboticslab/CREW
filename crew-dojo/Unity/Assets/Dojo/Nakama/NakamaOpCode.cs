namespace Dojo.Nakama
{
    /// <summary>
    /// Unique message identifiers used in Dojo
    /// </summary>
    public enum NakamaOpCode
    {
        /** Join match request from client */
        HelloFromClient = -10,

        /** Broadcast presence from server */
        HelloFromServer = -9,

        /** Server announces updates on current clients in match */
        UpdateClients = -8,

        /** Client requests to switch role */
        SwitchRole = -7,

        /** Netcode transport messages \see \link Dojo.Netcode.DojoTransport DojoTransport \endlink */
        TransportMessages = -6,

        /** RTT Sync from server */
        RTTSync = -5,

        /** RTT Ack response from client */
        RTTAck = -4,

        /** Final response to compute RTT */
        RTTAckSync = -3,

        /** Newly joined client query existing recording clients */
        RecordQuery = -20,

        /** Recording client announces start of recording */
        RecordStart = -21,

        /** Recording client announces stop of recording */
        RecordStop = -22,

        /** New recording event data */
        RecordEvent = -23,
    }
}
