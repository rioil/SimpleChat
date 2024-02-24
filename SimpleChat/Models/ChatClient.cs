using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleChat.Models
{
    public class ChatClient : IAsyncDisposable
    {
        private TcpClient? _client;
        private readonly IPEndPoint _peer;
        private readonly Pipe _pipe = new();
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;

        public IPEndPoint Peer => _peer;

        public event Action<Message>? MessageReceived;

        public ChatClient(TcpClient client)
        {
            _client = client;
            if (client.Client.RemoteEndPoint is not IPEndPoint peer)
            {
                throw new ArgumentException("RemoteEndPoint must be IPEndPoint.", nameof(client));
            }
            _peer = peer;
            _cts = new CancellationTokenSource();
            _receiveTask = StartReceiveTask(_cts.Token);
        }

        public ChatClient(string address, int port)
        {
            _peer = new(IPAddress.Parse(address), port);
        }

        public async Task ConnectAsync()
        {
            _client = new();
            await _client.ConnectAsync(_peer).ConfigureAwait(false);
            _cts = new CancellationTokenSource();
            _receiveTask = StartReceiveTask(_cts.Token);
        }

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            if (_receiveTask is not null)
            {
                await _receiveTask.ConfigureAwait(false);
            }
            _cts?.Dispose();
            _cts = null;

            _client?.Close();
            _client = null;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
        }

        public async Task SendMessageAsync(Message message)
        {
            if (_client is null)
            {
                throw new InvalidOperationException();
            }
            var stream = _client.GetStream();
            await stream.WriteAsync(message.Serialize());
            Debug.WriteLine($"{nameof(SendMessageAsync)}: {message.Content}");
        }

        private async Task StartReceiveTask(CancellationToken cancellationToken)
        {
            if (_client is null) { throw new InvalidOperationException(); }
            var stream = _client.GetStream();
            var fillTask = StartFillTask(stream, _pipe.Writer, cancellationToken);
            var parseTask = StartParseTask(_pipe.Reader, cancellationToken);
            await Task.WhenAll(fillTask, parseTask);
        }

        private static Task StartFillTask(NetworkStream stream, PipeWriter writer, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!stream.DataAvailable)
                    {
                        await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var buffer = writer.GetMemory(256);
                    var readBytes = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (readBytes > 0)
                    {
                        writer.Advance(readBytes);
                    }

                    var result = await writer.FlushAsync(cancellationToken);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                await writer.CompleteAsync().ConfigureAwait(false);
            }, cancellationToken);
        }

        private Task StartParseTask(PipeReader reader, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await reader.ReadAsync(cancellationToken);
                    var buffer = result.Buffer;

                    while (TryReadMessage(ref buffer, out var message))
                    {
                        MessageReceived?.Invoke(message);
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                await reader.CompleteAsync().ConfigureAwait(false);
            }, cancellationToken);
        }

        private static bool TryReadMessage(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out Message? message)
        {
            var offset = 0;

            // check header
            const int headerLength = 2;
            ReadOnlySpan<byte> expectedHeader = [(byte)0x40, (byte)0x90];
            Span<byte> headerBytes = stackalloc byte[headerLength];
            if (!TryGetSliced(buffer, offset, headerLength, headerBytes))
            {
                message = default;
                return false;
            }
            if (!headerBytes.SequenceEqual(expectedHeader))
            {
                message = default;
                return false;
            }
            offset += headerLength;

            // get sender IPv4 address (4bytes)
            const int addressLength = 4;
            Span<byte> addressBytes = stackalloc byte[addressLength];
            if (!TryGetSliced(buffer, offset, addressLength, addressBytes))
            {
                message = default;
                return false;
            }
            var address = new IPAddress(addressBytes);
            offset += addressLength;

            // get length of message (4bytes)
            const int lengthLength = 4;
            Span<byte> lengthBytes = stackalloc byte[lengthLength];
            if (!TryGetSliced(buffer, offset, lengthLength, lengthBytes))
            {
                message = default;
                return false;
            }
            var contentLength = BitConverter.ToInt32(lengthBytes);
            if (contentLength < 0)
            {
                message = default;
                return false;
            }
            offset += lengthLength;

            // get content
            var contentBytes = ArrayPool<byte>.Shared.Rent(contentLength);
            if (!TryGetSliced(buffer, offset, contentLength, contentBytes))
            {
                message = default;
                return false;
            }
            offset += contentLength;
            var content = Encoding.UTF8.GetString(contentBytes);

            // return message
            buffer = buffer.Slice(buffer.GetPosition(offset, buffer.Start));
            message = new Message(address, content);
            return true;

            static bool TryGetSliced(ReadOnlySequence<byte> buffer, int offset, int length, Span<byte> sliced)
            {
                if (buffer.Length < offset + length)
                {
                    return false;
                }
                var slicedBuffer = buffer.Slice(offset, length);
                slicedBuffer.CopyTo(sliced);
                return true;
            }
        }
    }
}
