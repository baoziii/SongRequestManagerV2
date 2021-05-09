﻿using BeatSaberMarkupLanguage.Attributes;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Twitch;
using ChatCore.Models.BiliBili;
using ChatCore.Utilities;
using HMUI;
using SongCore;
using SongRequestManagerV2.Bases;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.Utils;
using System;
using System.Collections.Concurrent;
using TMPro;
using UnityEngine;
using Zenject;

namespace SongRequestManagerV2
{
    public class SongRequest : BindableBase
    {
        [UIComponent("coverImage")]
        public ImageView _coverImage;

        [UIComponent("songNameText")]
        public TextMeshProUGUI _songNameText;

        [UIComponent("authorNameText")]
        public TextMeshProUGUI _authorNameText;

        [Inject]
        private readonly DynamicText.DynamicTextFactory _textFactory;

        /// <summary>説明 を取得、設定</summary>
        private string hint_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("hover-hint")]
        public string Hint
        {
            get => this.hint_ ?? "";

            set => this.SetProperty(ref this.hint_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string songName_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("song-name")]
        public string SongName
        {
            get => this.songName_ ?? "";

            set => this.SetProperty(ref this.songName_, value);
        }

        /// <summary>説明 を取得、設定</summary>
        private string authorName_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("author-name")]
        public string AuthorName
        {
            get => this.authorName_ ?? "";

            set => this.SetProperty(ref this.authorName_, value);
        }

        public JSONObject _song;
        public IChatUser _requestor;
        public DateTime _requestTime;
        public RequestStatus _status;
        public string _requestInfo; // Contains extra song info, Like : Sub/Donation request, Deck pick, Empty Queue pick,Mapper request, etc.
        public string _songName;
        public string _authorName;

        private static readonly ConcurrentDictionary<string, Texture2D> _cachedTextures = new ConcurrentDictionary<string, Texture2D>();

        public SongRequest Init(JSONObject obj)
        {
            this.Init(
                obj["song"].AsObject,
                this.CreateRequester(obj),
                DateTime.FromFileTime(long.Parse(obj["time"].Value)),
                (RequestStatus)Enum.Parse(typeof(RequestStatus),
                obj["status"].Value),
                obj["requestInfo"].Value);
            return this;
        }

        public SongRequest Init(JSONObject song, IChatUser requestor, DateTime requestTime, RequestStatus status = RequestStatus.Invalid, string requestInfo = "")
        {
            this._song = song;
            this._songName = song["songName"].Value;
            this._authorName = song["levelAuthor"].Value;
            this._requestor = requestor;
            this._status = status;
            this._requestTime = requestTime;
            this._requestInfo = requestInfo;
            return this;
        }

        [UIAction("#post-parse")]
        internal void Setup()
        {
            if (RequestBotConfig.Instance.PPSearch && MapDatabase.PPMap.TryGetValue(this._song["key"].Value, out var pp) && 0 < pp) {
                this.SongName = $"{this._songName} <size=50%>{Utility.GetRating(this._song)} <color=#4169e1>{pp:0.00} PP</color></size>";
            }
            else {
                this.SongName = $"{this._songName} <size=50%>{Utility.GetRating(this._song)}</size>";
            }

            this.SetCover();
        }

        [UIAction("selected")]
        private void Selected() => Logger.Debug($"Selected : {this._songName}");

        [UIAction("hovered")]
        private void Hovered() => Logger.Debug($"Hovered : {this._songName}");

        [UIAction("un-selected-un-hovered")]
        private void UnSelectedUnHovered() => Logger.Debug($"UnSelectedUnHovered : {this._songName}");
        /// <summary>
        /// lookup song from level id
        /// </summary>
        /// <returns></returns>
        private IPreviewBeatmapLevel GetCustomLevel() => Loader.GetLevelByHash(this._song["hash"]);

        public void SetCover() => Dispatcher.RunOnMainThread(async () =>
                                {
                                    try {
                                        this._coverImage.enabled = false;
                                        var dt = this._textFactory.Create().AddSong(this._song).AddUser(this._requestor); // Get basic fields
                                        dt.Add("Status", this._status.ToString());
                                        dt.Add("Info", (this._requestInfo != "") ? " / " + this._requestInfo : "");
                                        dt.Add("RequestTime", this._requestTime.ToLocalTime().ToString("hh:mm"));
                                        this.AuthorName = dt.Parse(StringFormat.QueueListRow2);
                                        this.Hint = dt.Parse(StringFormat.SongHintText);

                                        var imageSet = false;

                                        if (SongCore.Loader.AreSongsLoaded) {
                                            var level = this.GetCustomLevel();
                                            if (level != null) {
                                                //Logger.Debug("custom level found");
                                                // set image from song's cover image
                                                var tex = await level.GetCoverImageAsync(System.Threading.CancellationToken.None);
                                                this._coverImage.sprite = tex;
                                                imageSet = true;
                                            }
                                        }

                                        if (!imageSet) {
                                            var url = this._song["coverURL"].Value;

                                            if (!_cachedTextures.TryGetValue(url, out var tex)) {
                                                var b = await WebClient.DownloadImage($"https://beatsaver.com{url}", System.Threading.CancellationToken.None).ConfigureAwait(true);

                                                tex = new Texture2D(2, 2);
                                                tex.LoadImage(b);

                                                try {
                                                    _cachedTextures.AddOrUpdate(url, tex, (s, v) => tex);
                                                }
                                                catch (Exception e) {
                                                    Logger.Error(e);
                                                }
                                            }
                                            this._coverImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                                        }
                                    }
                                    catch (Exception e) {
                                        Logger.Error(e);
                                    }
                                    finally {
                                        this._coverImage.enabled = true;
                                    }
                                });

        public JSONObject ToJson()
        {
            try {
                var obj = new JSONObject();
                obj.Add("status", new JSONString(this._status.ToString()));
                obj.Add("requestInfo", new JSONString(this._requestInfo));
                obj.Add("time", new JSONString(this._requestTime.ToFileTime().ToString()));
                obj.Add("requestor", this._requestor.ToJson());
                obj.Add("song", this._song);
                return obj;
            }
            catch (Exception ex) {
                Logger.Debug($"{ex}\r\n{ex.Message}");
                return null;
            }
        }

        private IChatUser CreateRequester(JSONObject obj)
        {
            try {
                var temp = new TwitchUser(obj["requestor"].AsObject.ToString());
                return temp;
            }
            catch (Exception e) {
                Logger.Debug($"{e}");
                return new UnknownChatUser(obj["requestor"].AsObject.ToString());
            }
        }

        public class SongRequestFactory : PlaceholderFactory<SongRequest>
        {

        }
    }
}