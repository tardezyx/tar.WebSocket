using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tar.WebSocket {
  public class WebSocketClient {
    private ClientWebSocket _clientWebSocket;
    private DateTime? _closed;
    private CancellationTokenSource _listenTokenSource;
    private DateTime? _opened;
    private readonly WebSocketClientOptions _options;
    private CancellationTokenSource _sendTokenSource;
    private WebSocketState? _state;
    private readonly string _url;

    public delegate void OnActionDelegate(WebSocketClientInfo info);
    public event OnActionDelegate OnAction;

    public WebSocketClient(
      string url,
      WebSocketClientOptions options = null
    ) {
      _options = options;
      _url = url;

      CreateClientWebSocket();
    }

    public void Abort() {
      CheckIfStateHasChanged();
      _listenTokenSource.Cancel();
      _sendTokenSource.Cancel();

      _clientWebSocket.Abort();
      _clientWebSocket = null;
      CheckIfStateHasChanged();
    }

    public void AddSubProtocol(string subProtocol) {
      if (_clientWebSocket is null) {
        CreateClientWebSocket();
      }

      _clientWebSocket.Options.AddSubProtocol(subProtocol);
    }

    private void Callback(
      WebSocketClientAction clientAction,
      string errorMessage = null,
      string receivedMessage = null,
      string sentMessage = null,
      object sentOptions = null,
      object sentPayload = null,
      bool success = false,
      bool triggeredByClient = true
    ) {
      if (clientAction != WebSocketClientAction.StateChanged) {
        CheckIfStateHasChanged();
      }

      var webSocketInfo = new WebSocketClientInfo() {
        ClientAction = clientAction,
        ClientActionDescription = clientAction.ToString(),
        Closed = _closed,
        Duration = _closed != null && _opened != null ? _closed - _opened : null,
        ErrorMessage = errorMessage,
        Opened = _opened,
        ReceivedMessage = receivedMessage,
        SentMessage = sentMessage,
        SentOptions = sentOptions,
        SentPayload = sentPayload,
        State = _clientWebSocket?.State,
        StateDescription = _clientWebSocket?.State is WebSocketState state ? state.ToString() : null,
        Success = success,
        Timestamp = DateTime.UtcNow,
        TriggeredByClient = triggeredByClient,
        Url = _url
      };

      OnAction?.Invoke(webSocketInfo);
    }

    private void CheckIfStateHasChanged() {
      if (_state != _clientWebSocket?.State) {
        _state = _clientWebSocket?.State;

        Callback(WebSocketClientAction.StateChanged);
      }
    }

    public async Task CloseAsync(string description = null) {
      CheckIfStateHasChanged();

      if (_clientWebSocket is null || _clientWebSocket.State != WebSocketState.Open) {
        Callback(
          clientAction: WebSocketClientAction.Closing,
          errorMessage: "Web socket is not open."
        );
        return;
      }

      _sendTokenSource?.Cancel();

      // when the token is cancelled, ListenForReceiveAsync leaves an invalid socket (state = Aborted)
      // => close the socket first
      var timeout = new CancellationTokenSource(3000);
      try {
        await _clientWebSocket.CloseOutputAsync(
          WebSocketCloseStatus.NormalClosure,
          description,
          timeout.Token
        );
        // => socket state is now CloseSent

        while (
          _clientWebSocket != null
          && _clientWebSocket.State != WebSocketState.Closed
          && !timeout.Token.IsCancellationRequested
        ) {
          // wait for the server response, which will close the socket
          CheckIfStateHasChanged();
        };
      } catch (Exception ex) {
        Callback(
          clientAction: WebSocketClientAction.Closing,
          errorMessage: ex.Message
        );
        return;
      }

      // whether we closed the socket or timed out, we cancel the token
      // => ListenForReceiveAsync aborts the socket
      _listenTokenSource?.Cancel();
      // => the finally block at the end of ListenForReceiveAsync will dispose and nullify the socket object

      _closed = DateTime.UtcNow;

      Callback(
        clientAction: WebSocketClientAction.Closing,
        success: true
      );
    }

    public async Task ConnectAsync() {
      CheckIfStateHasChanged();

      if (
        _clientWebSocket is null
        || _clientWebSocket.State == WebSocketState.Aborted
        || _clientWebSocket.State == WebSocketState.Closed
      ) {
        CreateClientWebSocket();
      }

      if (_clientWebSocket.State == WebSocketState.Open) {
        Callback(
          clientAction: WebSocketClientAction.Connecting,
          errorMessage: "Web socket connection is already open."
        );

        return;
      }

      try {
        await _clientWebSocket.ConnectAsync(
          new Uri(_url),
          CancellationToken.None
        );

        _opened = DateTime.UtcNow;
      } catch (Exception ex) {
        Callback(
          clientAction: WebSocketClientAction.Connecting,
          errorMessage: ex.Message
        );

        return;
      }

      Callback(
        clientAction: WebSocketClientAction.Connecting,
        success: true
      );

      await ListenForReceiveAsync();
    }

    private void CreateClientWebSocket() {
      _clientWebSocket = new ClientWebSocket();

      if (_options is null) {
        _clientWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(1);
      } else {
        if (_options.ClientCertificates != null) {
          _clientWebSocket.Options.ClientCertificates = _options.ClientCertificates;
        }

        if (_options.Cookies != null) {
          _clientWebSocket.Options.Cookies = _options.Cookies;
        }

        if (_options.KeepAliveInterval is TimeSpan keepAliveInterval) {
          _clientWebSocket.Options.KeepAliveInterval = keepAliveInterval;
        } else {
          _clientWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(1);
        }

        if (_options.Credentials != null) {
          _clientWebSocket.Options.Credentials = _options.Credentials;
        }

        if (_options.Proxy != null) {
          _clientWebSocket.Options.Proxy = _options.Proxy;
        }

        if (_options.UseDefaultCredentials is bool useDefaultCredentials) {
          _clientWebSocket.Options.UseDefaultCredentials = useDefaultCredentials;
        }
      }

      _listenTokenSource = new CancellationTokenSource();
      _sendTokenSource = new CancellationTokenSource();

      CheckIfStateHasChanged();
    }

    private async Task ListenForReceiveAsync() {
      var memoryStream = new MemoryStream();
      var messageBuffer = new ArraySegment<byte>(new byte[4096]);
      WebSocketReceiveResult receiveResult;

      try {
        while (
          _clientWebSocket != null
          && _clientWebSocket.State != WebSocketState.Closed
          && _listenTokenSource != null
          && !_listenTokenSource.Token.IsCancellationRequested
        ) {
          do {
            receiveResult = await _clientWebSocket.ReceiveAsync(messageBuffer, _listenTokenSource.Token);
            memoryStream.Write(messageBuffer.Array, messageBuffer.Offset, receiveResult.Count);
          } while (!receiveResult.EndOfMessage);
          
          CheckIfStateHasChanged();

          // if the token is cancelled while ReceiveAsync is blocking,
          // the socket state changes to aborted and it cannot be used
          if (!_listenTokenSource.Token.IsCancellationRequested) {
            if (
              _clientWebSocket.State == WebSocketState.CloseReceived
              && receiveResult.MessageType == WebSocketMessageType.Close
            ) {
              // the server is notifying us that the connection will close
              _sendTokenSource?.Cancel();

              // acknowledging the received close frame from server
              await _clientWebSocket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "Acknowledge close frame",
                CancellationToken.None
              );

              Callback(
                clientAction: WebSocketClientAction.Closing,
                triggeredByClient: false,
                success: true
              );
            }

            // handle received text or binary data
            if (
              _clientWebSocket.State == WebSocketState.Open
              && receiveResult.MessageType != WebSocketMessageType.Close
            ) {
              string jsonMessage = Encoding.UTF8.GetString(memoryStream.ToArray());

              Callback(
                clientAction: WebSocketClientAction.MessageReceived,
                triggeredByClient: false,
                receivedMessage: jsonMessage
              );
            }
          }

          memoryStream.SetLength(0);
          CheckIfStateHasChanged();
        }
      } catch (OperationCanceledException) {
        // disregard as the exception is normal upon task/token cancellation
      } catch {
        // disregard when aborted (= connection lost)
        if (_clientWebSocket?.State != WebSocketState.Aborted) {
          // throw all other exceptions
          throw;
        }
      } finally {
        _sendTokenSource?.Cancel();

        CheckIfStateHasChanged();
        // => state is Closed or Aborted
        
        // a closed/aborted ClientWebSocket cannot be re-used
        // => dispose and nullify it
        _clientWebSocket?.Dispose();
        _clientWebSocket = null;

        CheckIfStateHasChanged();
        // => empty state

        memoryStream.Dispose();
      }
    }

    public async Task SendAsync(
      string payloadJson,
      object options = null,
      object payload = null
    ) {
      string errorMessage = null;

      if (
        _clientWebSocket?.State == WebSocketState.Open
        && _sendTokenSource != null
        && !_sendTokenSource.Token.IsCancellationRequested
      ) {
        try {
          byte[] buffer = Encoding.UTF8.GetBytes(payloadJson);

          await _clientWebSocket.SendAsync(
            new ArraySegment<byte>(buffer),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
          );
        } catch (OperationCanceledException) {
          // disregard as the exception is normal upon task/token cancellation
        } catch (Exception ex) {
          errorMessage = ex.Message;
        }
      } else {
        errorMessage = "Web socket connection is not ready to send.";
      }

      Callback(
        clientAction: WebSocketClientAction.MessageSent,
        errorMessage: errorMessage,
        sentMessage: payloadJson,
        sentOptions: options,
        sentPayload: payload,
        success: errorMessage == null
      );
    }

    public void SetBuffer(int receiveBufferSize, int sendBufferSize) {
      if (_clientWebSocket is null) {
        CreateClientWebSocket();
      }

      _clientWebSocket.Options.SetBuffer(receiveBufferSize, sendBufferSize);
    }

    public void SetBuffer(int receiveBufferSize, int sendBufferSize, ArraySegment<byte> buffer) {
      if (_clientWebSocket is null) {
        CreateClientWebSocket();
      }

      _clientWebSocket.Options.SetBuffer(receiveBufferSize, sendBufferSize, buffer);
    }

    public void SetRequestHeader(string headerName, string headerValue) {
      if (_clientWebSocket is null) {
        CreateClientWebSocket();
      }

      _clientWebSocket.Options.SetRequestHeader(headerName, headerValue);
    }
  }
}