// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Dolittle.Execution;
using Dolittle.Logging;
using Dolittle.Protobuf;
using Dolittle.Services.Contracts;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Dolittle.Services.Clients
{
    /// <summary>
    /// Represents an implementation of <see cref="IReverseCallClient{TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TClientMessage">Type of the <see cref="IMessage">messages</see> that is sent from the client to the server.</typeparam>
    /// <typeparam name="TServerMessage">Type of the <see cref="IMessage">messages</see> that is sent from the server to the client.</typeparam>
    /// <typeparam name="TConnectArguments">Type of the arguments that are sent along with the initial Connect call.</typeparam>
    /// <typeparam name="TConnectResponse">Type of the response that is received after the initial Connect call.</typeparam>
    /// <typeparam name="TRequest">Type of the requests sent from the server to the client using <see cref="IReverseCallDispatcher{TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse}.Call"/>.</typeparam>
    /// <typeparam name="TResponse">Type of the responses received from the client using <see cref="IReverseCallDispatcher{TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse}.Call"/>.</typeparam>
    public class ReverseCallClient<TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse>
        : IDisposable, IReverseCallClient<TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse>
        where TClientMessage : IMessage, new()
        where TServerMessage : IMessage, new()
        where TConnectArguments : class
        where TConnectResponse : class
        where TRequest : class
        where TResponse : class
    {
        readonly Func<AsyncDuplexStreamingCall<TClientMessage, TServerMessage>> _establishConnection;
        readonly Action<TConnectArguments, ReverseCallArgumentsContext> _setArgumentsContext;
        readonly Action<TClientMessage, TConnectArguments> _setConnectArguments;
        readonly Func<TServerMessage, TConnectResponse> _getConnectResponse;
        readonly Func<TServerMessage, TRequest> _getMessageRequest;
        readonly Func<TRequest, ReverseCallRequestContext> _getRequestContext;
        readonly Action<TResponse, ReverseCallResponseContext> _setResponseContext;
        readonly Func<TServerMessage, Ping> _getPing;
        readonly Action<TClientMessage, Pong> _setPong;
        readonly TimeSpan _pingInterval;
        readonly Action<TClientMessage, TResponse> _setMessageResponse;
        readonly IExecutionContextManager _executionContextManager;
        readonly ILogger _logger;
        readonly SemaphoreSlim _writeResponseSemaphore = new SemaphoreSlim(1);
        readonly object _connectLock = new object();
        readonly object _handleLock = new object();
        IClientStreamWriter<TClientMessage> _clientToServer;
        IAsyncStreamReader<TServerMessage> _serverToClient;
        bool _connecting;
        bool _connectionEstablished;
        bool _startedHandling;
        bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReverseCallClient{TClientMessage, TServerMessage, TConnectArguments, TConnectResponse, TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="establishConnection">The <see cref="AsyncDuplexStreamingCall{TRequest, TResponse}" />.</param>
        /// <param name="setConnectArguments">A delegate to set the <typeparamref name="TConnectArguments" /> on a <typeparamref name="TClientMessage" />.</param>
        /// <param name="getConnectResponse">A delegate to get the <typeparamref name="TConnectResponse" /> from a <typeparamref name="TServerMessage" />.</param>
        /// <param name="getMessageRequest">A delegate to get the <typeparamref name="TRequest" /> from the <typeparamref name="TServerMessage" />.</param>
        /// <param name="setMessageResponse">A delegate to set the <typeparamref name="TResponse" /> on a <typeparamref name="TClientMessage" />.</param>
        /// <param name="setArgumentsContext">A delegate to set the <see cref="ReverseCallArgumentsContext" /> on a <typeparamref name="TConnectArguments" />.</param>
        /// <param name="getRequestContext">A delegate to get the <see cref="ReverseCallRequestContext" /> from the <typeparamref name="TRequest" />.</param>
        /// <param name="setResponseContext">A delegate to set the <see cref="ReverseCallResponseContext" /> on a <typeparamref name="TResponse" />.</param>
        /// <param name="getPing">A delegate to get the <see cref="Ping" /> from the <typeparamref name="TServerMessage" />.</param>
        /// <param name="setPong">A delegate to set the <see cref="Ping" /> on a <typeparamref name="TClientMessage" />.</param>
        /// <param name="pingInterval">A <see cref="TimeSpan" /> for the interval between pings from the server.</param>
        /// <param name="executionContextManager">The <see cref="IExecutionContextManager" />.</param>
        /// <param name="logger">The <see cref="ILogger" />.</param>
        public ReverseCallClient(
            Func<AsyncDuplexStreamingCall<TClientMessage, TServerMessage>> establishConnection,
            Action<TClientMessage, TConnectArguments> setConnectArguments,
            Func<TServerMessage, TConnectResponse> getConnectResponse,
            Func<TServerMessage, TRequest> getMessageRequest,
            Action<TClientMessage, TResponse> setMessageResponse,
            Action<TConnectArguments, ReverseCallArgumentsContext> setArgumentsContext,
            Func<TRequest, ReverseCallRequestContext> getRequestContext,
            Action<TResponse, ReverseCallResponseContext> setResponseContext,
            Func<TServerMessage, Ping> getPing,
            Action<TClientMessage, Pong> setPong,
            TimeSpan pingInterval,
            IExecutionContextManager executionContextManager,
            ILogger logger)
        {
            ThrowIfInvalidPingInterval(pingInterval);
            _establishConnection = establishConnection;
            _setConnectArguments = setConnectArguments;
            _getConnectResponse = getConnectResponse;
            _getMessageRequest = getMessageRequest;
            _setMessageResponse = setMessageResponse;
            _setArgumentsContext = setArgumentsContext;
            _getRequestContext = getRequestContext;
            _setResponseContext = setResponseContext;
            _getPing = getPing;
            _setPong = setPong;
            _pingInterval = pingInterval;
            _executionContextManager = executionContextManager;
            _logger = logger;
        }

        /// <inheritdoc/>
        public TConnectResponse ConnectResponse { get; private set; }

        /// <inheritdoc/>
        public async Task<bool> Connect(TConnectArguments connectArguments, CancellationToken cancellationToken)
        {
            ThrowIfConnecting();
            lock (_connectLock)
            {
                ThrowIfConnecting();
                _connecting = true;
            }

            var streamingCall = _establishConnection();
            _clientToServer = streamingCall.RequestStream;
            _serverToClient = streamingCall.ResponseStream;
            var callContext = new ReverseCallArgumentsContext
                {
                    ExecutionContext = _executionContextManager.Current.ToProtobuf(),
                    PingInterval = Duration.FromTimeSpan(_pingInterval)
                };
            _setArgumentsContext(connectArguments, callContext);
            var message = new TClientMessage();
            _setConnectArguments(message, connectArguments);

            await _clientToServer.WriteAsync(message).ConfigureAwait(false);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_pingInterval.Multiply(3));

            if (await _serverToClient.MoveNext(linkedCts.Token).ConfigureAwait(false))
            {
                var response = _getConnectResponse(_serverToClient.Current);
                if (response != null)
                {
                    _logger.Trace("Received connect response");
                    ConnectResponse = response;
                    _connectionEstablished = true;
                    return true;
                }
                else
                {
                    _logger.Warning("Did not receive connect response. Server message did not contain the connect response");
                }
            }
            else
            {
                _logger.Warning("Did not receive connect response. Server stream was empty");
            }

            await _clientToServer.CompleteAsync().ConfigureAwait(false);
            return false;
        }

        /// <inheritdoc/>
        public async Task Handle(Func<TRequest, CancellationToken, Task<TResponse>> callback, CancellationToken cancellationToken)
        {
            ThrowIfConnectionNotEstablished();
            ThrowIfAlreadyStartedHandling();
            lock (_handleLock)
            {
                ThrowIfAlreadyStartedHandling();
                _startedHandling = true;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_pingInterval.Multiply(3));
            while (await _serverToClient.MoveNext(linkedCts.Token).ConfigureAwait(false))
            {
                var message = _serverToClient.Current;
                var ping = _getPing(message);
                var request = _getMessageRequest(message);
                if (ping != null)
                {
                    _logger.Trace("Received ping");
                    await WritePong(cancellationToken).ConfigureAwait(false);
                }
                else if (request != null)
                {
                    _ = Task.Run(() => HandleRequest(callback, request, cancellationToken));
                }
                else
                {
                    _logger.Warning("Received message from reverse call dispatcher, but it was not a request or a ping");
                }

                linkedCts.CancelAfter(_pingInterval.Multiply(3));
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose the managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">Whether to dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _writeResponseSemaphore.Dispose();
                }

                _disposed = true;
            }
        }

        async Task WritePong(CancellationToken cancellationToken)
        {
            await _writeResponseSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Debug("Reverse call client was cancelled before it could respond with pong");
                    return;
                }

                var message = new TClientMessage();
                _setPong(message, new Pong());

                _logger.Trace("Writing pong");
                await _clientToServer.WriteAsync(message).ConfigureAwait(false);
            }
            finally
            {
                _writeResponseSemaphore.Release();
            }
        }

        async Task HandleRequest(Func<TRequest, CancellationToken, Task<TResponse>> callback, TRequest request, CancellationToken cancellationToken)
        {
            var requestContext = _getRequestContext(request);
            _logger.Trace("Handling request with call {callId}", requestContext.CallId.To<ReverseCallId>());
            _executionContextManager.CurrentFor(requestContext.ExecutionContext);

            var response = await callback(request, cancellationToken).ConfigureAwait(false);

            await _writeResponseSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var responseContext = new ReverseCallResponseContext { CallId = requestContext.CallId };
                _setResponseContext(response, responseContext);
                var message = new TClientMessage();
                _setMessageResponse(message, response);
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Debug("Reverse call cancelled before request '{callId}' could be handled", requestContext.CallId.To<ReverseCallId>());
                    return;
                }

                _logger.Trace("Writing response request with call {callId}", responseContext.CallId.To<ReverseCallId>());
                await _clientToServer.WriteAsync(message).ConfigureAwait(false);
            }
            finally
            {
                _writeResponseSemaphore.Release();
            }
        }

        void ThrowIfInvalidPingInterval(TimeSpan pingInterval)
        {
            if (pingInterval.TotalMilliseconds <= 0) throw new PingIntervalNotGreaterThanZero();
        }

        void ThrowIfConnecting()
        {
            if (_connecting)
            {
                throw new ReverseCallClientAlreadyCalledConnect();
            }
        }

        void ThrowIfAlreadyStartedHandling()
        {
            if (_startedHandling)
            {
                throw new ReverseCallClientAlreadyStartedHandling();
            }
        }

        void ThrowIfConnectionNotEstablished()
        {
            if (!_connectionEstablished)
            {
                throw new ReverseCallClientNotConnected();
            }
        }
    }
}