﻿using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Services;
using ChatCore.Services.Twitch;
using SongRequestManagerV2.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SongRequestManagerV2.Utils
{
    public class ChatManager : IChatManager
    {
        public ChatCoreInstance CoreInstance { get; private set; }
        public ChatServiceMultiplexer MultiplexerInstance { get; private set; }
        public TwitchService TwitchService { get; private set; }

        public ConcurrentQueue<IChatMessage> RecieveChatMessage { get; } = new ConcurrentQueue<IChatMessage>();
        public ConcurrentQueue<RequestInfo> RequestInfos { get; } = new ConcurrentQueue<RequestInfo>();
        public ConcurrentQueue<string> SendMessageQueue { get; } = new ConcurrentQueue<string>();

        public void Initialize()
        {
            this.CoreInstance = ChatCoreInstance.Create();
            this.MultiplexerInstance = this.CoreInstance.RunAllServices();
            this.MultiplexerInstance.OnLogin -= this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnLogin += this.MultiplexerInstance_OnLogin;
            this.MultiplexerInstance.OnJoinChannel -= this.MultiplexerInstance_OnJoinChannel;
            this.MultiplexerInstance.OnJoinChannel += this.MultiplexerInstance_OnJoinChannel;
            this.TwitchService = this.MultiplexerInstance.GetTwitchService();
            this.MultiplexerInstance.OnTextMessageReceived += this.MultiplexerInstance_OnTextMessageReceived;
        }

        /// <summary>
        /// メッセージを送信キューへ追加します。
        /// </summary>
        /// <param name="message">ストリームサービスへ送信したい文字列</param>
        public void QueueChatMessage(string message)
        {
            this.SendMessageQueue.Enqueue($"{RequestBotConfig.Instance.BotPrefix}\uFEFF{message}");
        }

        private void MultiplexerInstance_OnTextMessageReceived(IChatService arg1, IChatMessage arg2)
        {
            this.RecieveChatMessage.Enqueue(arg2);
        }

        void MultiplexerInstance_OnJoinChannel(IChatService arg1, IChatChannel arg2)
        {
            Logger.Debug($"Joined! : [{arg1.DisplayName}][{arg2.Name}]");
            if (arg1 is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }

        void MultiplexerInstance_OnLogin(IChatService obj)
        {
            Logger.Debug($"Loged in! : [{obj.DisplayName}]");
            if (obj is TwitchService twitchService) {
                this.TwitchService = twitchService;
            }
        }
    }
}
