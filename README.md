# tar.WebSocket

 - [X] C# .NET Standard v2.0

## Function

This library can be used as web socket client.

It basically wraps the functionality of `System.Net.WebSockets.ClientWebSocket` but additionally provides:

<ul>
  <li>the correct close handling when the client or the server triggers the closure</li>
  <li>re-connection after the web socket state has once been set to `Closed` or `Aborted`</li>
  <li>an `OnAction` event which can be subscribed for callbacks whenever any of the following action occurs
    <ul>
      <li>Connecting</li>
      <li>Closing</li>
      <li>MessageReceived</li>
      <li>MessageSent</li>
      <li>StateChanged</li>
    </ul>
  </li>
</ul>

## Usage

```cs
var options = new WebSocketClientOptions() {
  KeepAliveInterval = TimeSpan.FromSeconds(5);
};

var webSocketClient = new WebSocketClient(
  options: options // optional
  url: "wss://ws.example.com/v1/" // obligatory
);
```

You can set the options as usual and they will be adopted.
If no options or no `KeepAliveInterval` are set, a `KeepAliveInterval` of 1 second will be used.

After instanciation you cannot adjust the options but you can use the usual options methods on the client directly:

<ul>
  <li>`AddSubProtocol(string subProtocol)`</li>
  <li>`SetBuffer(int receiveBufferSize, int sendBufferSize)`</li>
  <li>`SetBuffer(int receiveBufferSize, int sendBufferSize, ArraySegment<byte> buffer)`</li>
  <li>`SetRequestHeader(string headerName, string headerValue)`</li>
</ul>

Be aware, that you may still need to re-call those after an existing connection has been aborted/closed.

Ohterwise, you can use the web socket client as usual:

<ul>
  <li>connect via `await webSocketClient.ConnectAsync();`</li>
  <li>close via `await webSocketClient.CloseAsync();`</li>
  <li>send via `await webSocketClient.SendAsync(jsonString);`</li>
  <li>abort via `webSocketClient.Abort();`</li>
  <li>receive updates via subscribing to the event `OnAction`</li>
</ul>

You are able to forward an optional optionsObject and payloadObject to `SendAsync()` which are returned via callback in the `OnAction` event.

To receive updates, you need to add an event handler for the event `OnAction` and subscribe it.

For example:

```cs
webSocketClient.OnAction += OnWebSocketClientAction;

private void OnWebSocketClientAction(WebSocketClientInfo info) {
  switch (info.ClientAction) {
    case WebSocketClientAction.Closing: OnClosing(info); break;
    case WebSocketClientAction.Connecting: OnConnecting(info); break;
    case WebSocketClientAction.MessageReceived: OnMessageReceived(info); break;
    case WebSocketClientAction.MessageSent: OnMessageSent(info); break;
    case WebSocketClientAction.StateChanged: OnStateChanged(info); break;
  }
}

private void OnClosing(WebSocketClientInfo info) {
  MessageBox.Show(
    info.Success
      ? "Connection closed"
      : $"Connection not closed: {info.ErrorMessage}"
  );
}

// etc.
```

The `WebSocketClientInfo` class contains all necessary information:

<ul>
  <li>ClientAction: action which triggered the callback</li>
  <li>ClientActionDescription: action as text</li>
  <li>Closed: last closed timestamp</li>
  <li>Duration: time the web socket is/was open</li>
  <li>ErrorMessage: description when an error occured</li>
  <li>Opened: last opened timestamp</li>
  <li>ReceivedMessage: received message as JSON string</li>
  <li>SentMessage: sent message as JSON string</li>
  <li>SentOptions: optional options object you have provided in `SendAsync()`</li>
  <li>SentPayload: optional payload object you have provided in `SendAsync()`</li>
  <li>State: the current state of the internal ClientWebSocket</li>
  <li>StateDescription: state as text</li>
  <li>Success: if the action was successful</li>
  <li>Timestamp: when the action occured</li>
  <li>TriggeredByClient: if the action was triggered by the client (you), otherwise by the server</li>
  <li>Url: the URL the web socket is connected to</li>
</ul>
