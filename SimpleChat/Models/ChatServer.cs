using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SimpleChat.Models
{
    public class ChatServer(int port = ChatServer.DefaultPort)
    {
        private readonly TcpListener _listener = new(IPAddress.Any, port);
        private readonly List<ChatClient> _connectedUsers = [];

        private readonly Channel<Message> _messageChannel = Channel.CreateUnbounded<Message>();

        private Task? _listenTask;
        private Task? _deliveryTask;
        private CancellationTokenSource? _cts;

        public const int DefaultPort = 10080;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listenTask = StartListen(_cts.Token);
            _deliveryTask = StartDelivery(_messageChannel.Reader, _cts.Token);
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_listenTask is not null)
            {
                await _listenTask;
            }
            if (_deliveryTask is not null)
            {
                await _deliveryTask;
            }
            _listener?.Stop();
        }

        private async Task StartListen(CancellationToken cancellationToken)
        {
            _listener.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                var chatClient = new ChatClient(tcpClient);
                _connectedUsers.Add(chatClient);
                chatClient.MessageReceived += OnMessageReceived;
                Debug.WriteLine($"{nameof(StartListen)}: Client connected {tcpClient.Client.RemoteEndPoint}");
            }
        }

        private async Task StartDelivery(ChannelReader<Message> reader, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    break;
                }

                foreach (var user in _connectedUsers)
                {
                    await user.SendMessageAsync(result).ConfigureAwait(false);
                }
            }
        }

        private void OnMessageReceived(Message message)
        {
            _messageChannel.Writer.TryWrite(message);
            Debug.WriteLine($"{nameof(OnMessageReceived)}: Message Received {message.SenderId} {message.Content}");
        }
    }
}
