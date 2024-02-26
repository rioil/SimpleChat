using Livet;
using Livet.Commands;
using SimpleChat.Models;
using System;
using System.Collections.ObjectModel;

namespace SimpleChat.ViewModels
{
    public class ChatViewModel : ViewModel
    {
        private readonly ChatClient _chatClient = new("127.0.0.1", ChatServer.DefaultPort);

        public int MyId { get; set; } = Random.Shared.Next();

        public ObservableCollection<Message> Messages
        {
            get => _messages;
            set => RaisePropertyChangedIfSet(ref _messages, value);
        }
        private ObservableCollection<Message> _messages = [];

        public string Content
        {
            get => _content;
            set
            {
                if (RaisePropertyChangedIfSet(ref _content, value))
                {
                    SendMessageCommand.RaiseCanExecuteChanged();
                }
            }
        }
        private string _content = string.Empty;

        public ChatViewModel()
        {
            _ = _chatClient.ConnectAsync();
            _chatClient.MessageReceived += OnMessageReceived;
        }

        public async void SendMessage()
        {
            await _chatClient.SendMessageAsync(new Message(MyId, DateTime.Now, Content));
            Content = string.Empty;
        }
        private bool CanSendMessage()
        {
            return !string.IsNullOrEmpty(Content);
        }
        public ViewModelCommand SendMessageCommand => _sendMessageCommand ??= new ViewModelCommand(SendMessage, CanSendMessage);
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
