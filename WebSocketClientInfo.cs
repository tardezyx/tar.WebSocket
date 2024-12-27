using System;
using System.Net.WebSockets;

namespace tar.WebSocket {
  public class WebSocketClientInfo {
    public WebSocketClientAction ClientAction { get; set; }
    public string ClientActionDescription { get; set; } = string.Empty;
    public DateTime? Closed { get; set; }
    public TimeSpan? Duration { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime? Opened { get; set; }
    public string ReceivedMessage { get; set; }
    public string SentMessage { get; set; }
    public object SentOptions { get; set; }
    public object SentPayload { get; set; }
    public WebSocketState? State { get; set; }
    public string StateDescription { get; set; }
    public bool Success { get; set; } = false;
    public DateTime Timestamp { get; set; }
    public bool TriggeredByClient { get; set; } = true;
    public string Url { get; set; } = string.Empty;
  }
}