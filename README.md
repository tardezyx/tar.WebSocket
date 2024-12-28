# tar.WebSocket

 - [X] C# .NET Standard v2.0

## Function

This library can be used as web socket client.

It basically wraps the functionality of `System.Net.WebSockets.ClientWebSocket` but additionally provides:

- the correct close handling when the client or the server triggers the closure
- the possibility to re-connect after the web socket state has once been set to `Closed` or `Aborted`
- an `OnAction` event which can be subscribed for callbacks whenever any of the following action occurs:
  - Closing
  - Connecting
  - MessageReceived
  - MessageSent
  - StateChanged

## Usage

```cs
var webSocketClient = new WebSocketClient(
  url: "wss://ws.example.com/v1/"
);
```

You can use the web socket client as usual:

```cs
await webSocketClient.ConnectAsync();
await webSocketClient.CloseAsync();
await webSocketClient.SendAsync(jsonString);
webSocketClient.Abort();
```

You are able to forward an optional optionsObject and payloadObject to `SendAsync()` which are returned via callback in the `OnAction` event.

### Options

Additionaly, you can set options as usual and they will be adopted.

```cs
var options = new WebSocketClientOptions() {
  KeepAliveInterval = TimeSpan.FromSeconds(5);
};

var webSocketClient = new WebSocketClient(
  options: options
  url: "wss://ws.example.com/v1/"
);
```

If no options or no `KeepAliveInterval` are set, a `KeepAliveInterval` of 1 second will be used.

After instanciation you cannot adjust the options but you can use the usual options methods on the client directly:

```cs
webSocketClient.AddSubProtocol(subProtocol);
webSocketClient.SetBuffer(receiveBufferSize, sendBufferSize);
webSocketClient.SetBuffer(receiveBufferSize, sendBufferSize, buffer);
webSocketClient.SetRequestHeader(headerName, headerValue);
```

Be aware, that you may still need to re-call those after an existing connection has been aborted/closed.

### Callback Event OnAction

To receive updates, you need to add an event handler for the event `OnAction` and subscribe it.

For example:

```cs
// register/subscribe to event
webSocketClient.OnAction += OnWebSocketClientAction;

// your callback method which is triggered via the event
private void OnWebSocketClientAction(WebSocketClientInfo info) {
  switch (info.ClientAction) {
    case WebSocketClientAction.Closing: OnClosing(info); break;
    case WebSocketClientAction.Connecting: OnConnecting(info); break;
    case WebSocketClientAction.MessageReceived: OnMessageReceived(info); break;
    case WebSocketClientAction.MessageSent: OnMessageSent(info); break;
    case WebSocketClientAction.StateChanged: OnStateChanged(info); break;
  }
}

// your explicit method where you handle on closing events
private void OnClosing(WebSocketClientInfo info) {
  MessageBox.Show(
    info.Success
      ? "Connection closed"
      : $"Connection not closed: {info.ErrorMessage}"
  );
}

// etc.
```

The returned `WebSocketClientInfo` class contains all necessary information:

- `ClientAction`: action which triggered the callback
- `ClientActionDescription`: action as text
- `Closed`: last closed timestamp
- `Duration`: time the web socket is/was open
- `ErrorMessage`: description when an error occured
- `Opened`: last opened timestamp
- `ReceivedMessage`: received message as JSON string
- `SentMessage`: sent message as JSON string
- `SentOptions`: optional options object you have provided in `SendAsync()`
- `SentPayload`: optional payload object you have provided in `SendAsync()`
- `State`: the current state of the internal ClientWebSocket
- `StateDescription`: state as text
- `Success`: if the action was successful
- `Timestamp`: when the action occured
- `TriggeredByClient`: if the action was triggered by the client (you), otherwise by the server
- `Url`: the URL the web socket is connected to

The provided information depends on the actual client action.