namespace OBSStudioClient
{
    using OBSStudioClient.Enums;

    /// <summary>
    /// Provides arguments for the ConnectionClosed event.
    /// </summary>
    public class ConnectionClosedEventArgs : EventArgs
    {
        /// <summary>
        /// Get the <see cref="WebSocketCloseCode"/> containing the reason for closing the session.
        /// </summary>
        public WebSocketCloseCode WebSocketCloseCode { get; }

        /// <summary>
        /// Gets the friendly description of the <see cref="WebSocketCloseCode"/>.
        /// </summary>
        public string WebSocketCloseDescription { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionClosedEventArgs"/> class.
        /// </summary>
        /// <param name="webSocketCloseCode">The <see cref="WebSocketCloseCode"/>.</param>
        /// <param name="webSocketCloseDescription">The friendly description.</param>
        internal ConnectionClosedEventArgs(WebSocketCloseCode webSocketCloseCode, string webSocketCloseDescription)
        {
            this.WebSocketCloseCode = webSocketCloseCode;
            this.WebSocketCloseDescription = webSocketCloseDescription;
        }
    }
}
