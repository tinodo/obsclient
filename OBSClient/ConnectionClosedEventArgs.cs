namespace OBSStudioClient
{
    using OBSStudioClient.Enums;

    public class ConnectionClosedEventArgs : EventArgs
    {
        public WebSocketCloseCode WebSocketCloseCode { get; private init; }

        public string WebSocketCloseDescription { get; private init; }

        internal ConnectionClosedEventArgs(WebSocketCloseCode webSocketCloseCode, string webSocketCloseDescription)
        {
            this.WebSocketCloseCode = webSocketCloseCode;
            this.WebSocketCloseDescription = webSocketCloseDescription;
        }
    }
}
