﻿using Livet;
using Livet.Commands;
using SimpleChat.Models;
using System.Collections.ObjectModel;
using System.Net;

namespace SimpleChat.ViewModels
{
    public class ChatViewModel : ViewModel
    {
        private readonly ChatClient _chatClient = new("127.0.0.1", ChatServer.DefaultPort);

        public ObservableCollection<Message> Messages
        {
            get => _messages;
            set => RaisePropertyChangedIfSet(ref _messages, value);
        }
        private ObservableCollection<Message> _messages = [];

        public string Content
        {
            get => _content;
            set => RaisePropertyChangedIfSet(ref _content, value);
        }
        private string _content = string.Empty;

        public ChatViewModel()
        {
            _ = _chatClient.ConnectAsync();
            _chatClient.MessageReceived += OnMessageReceived;
        }

        public async void SendMessage()
        {
            await _chatClient.SendMessageAsync(new Message(IPAddress.Loopback, Content));
            Content = string.Empty;
        }
        public ViewModelCommand SendMessageCommand => _sendMessageCommand ??= new ViewModelCommand(SendMessage);
        private ViewModelCommand? _sendMessageCommand;

        private void OnMessageReceived(Message message)
        {
            DispatcherHelper.UIDispatcher.Invoke(() =>
            {
                _messages.Add(message);
            });
        }
    }
}
