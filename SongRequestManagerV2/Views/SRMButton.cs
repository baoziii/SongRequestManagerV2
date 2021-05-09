﻿using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using BS_Utils.Utilities;
using HMUI;
using IPA.Utilities;
using SongCore;
using SongRequestManagerV2.Bots;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.Statics;
using SongRequestManagerV2.UI;
using SongRequestManagerV2.Utils;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRUIControls;
using Zenject;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class SRMButton : BSMLAutomaticViewController, IInitializable
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        [Inject]
        private readonly MainFlowCoordinator _mainFlowCoordinator;
        [Inject]
        private readonly LevelCollectionNavigationController levelCollectionNavigationController;
        [Inject]
        private readonly LevelSelectionNavigationController levelSelectionNavigationController;
        [Inject]
        private readonly RequestFlowCoordinator _requestFlow;
        [Inject]
        private readonly IRequestBot _bot;
        [Inject]
        private IChatManager ChatManager { get; }
        [Inject]
        private readonly DynamicText.DynamicTextFactory _textFactory;
        [Inject]
        private readonly StringNormalization Normalize;
        [Inject]
        private readonly SongListUtils SongListUtils;

        private Button _button;

        private readonly WaitForSeconds waitForSeconds = new WaitForSeconds(0.07f);

        private volatile bool isChangeing = false;
        private bool isInGame = false;
        public Progress<double> DownloadProgress { get; } = new Progress<double>();

        public HMUI.Screen Screen { get; set; }

        public Canvas ButtonCanvas { get; set; }

        public FlowCoordinator Current => this._mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

        private static readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(1, 1);

        [UIAction("action")]
        public void Action()
        {
            try {
                Logger.Debug("action");
                this._button.interactable = false;
                this.SRMButtonPressed();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                this._button.interactable = true;
            }

        }

        internal void SRMButtonPressed()
        {
            if (this.Current is LevelSelectionFlowCoordinator) {
                this.Current.PresentFlowCoordinator(this._requestFlow, null, AnimationDirection.Horizontal, false, false);
            }
            Logger.Debug($"{this.Current.name}");
            return;
        }

        internal void SetButtonColor()
        {
            if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1) {
                if (RequestManager.RequestSongs.Any()) {
                    Dispatcher.RunCoroutine(this.ChangeButtonColor());
                }
                else {
                    this.isChangeing = false;
                    var color = Color.red;
                    this._button.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = color;
                }
            }
            else {
                var color = RequestManager.RequestSongs.Any() ? Color.green : Color.red;
                this._button.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = color;
            }
            this._button.interactable = true;
        }

        internal void BackButtonPressed()
        {
            Logger.Debug($"{this.Current.name} : {this._requestFlow.name}");
            if (this.Current.name != this._requestFlow.name) {
                return;
            }
            try {
                this.Current.GetField<FlowCoordinator, FlowCoordinator>("_parentFlowCoordinator")?.DismissFlowCoordinator(this.Current, null, AnimationDirection.Horizontal, true);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
        public void Initialize()
        {
            Logger.Debug("Start()");

            this._bot.ChangeButtonColor += this.SetButtonColor;
            this._bot.RefreshListRequest += this.RefreshListRequest;
            this._requestFlow.QueueStatusChanged += this.OnQueueStatusChanged;
            this._requestFlow.PlayProcessEvent += this.ProcessSongRequest;

            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            this.DownloadProgress.ProgressChanged += this.Progress_ProgressChanged;
            SceneManager.activeSceneChanged += this.SceneManager_activeSceneChanged;
            try {
                var screen = new GameObject("SRMButton", typeof(CanvasScaler), typeof(RectMask2D), typeof(VRGraphicRaycaster), typeof(CurvedCanvasSettings));
                screen.GetComponent<VRGraphicRaycaster>().SetField("_physicsRaycaster", BeatSaberUI.PhysicsRaycasterWithCache);
                var vertical = screen.AddComponent<VerticalLayoutGroup>();
                var fitter = vertical.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                (screen.transform as RectTransform).sizeDelta = new Vector2(40f, 30f);
                (screen.transform as RectTransform).SetParent(this.levelCollectionNavigationController.transform as RectTransform, false);
                (screen.transform as RectTransform).anchoredPosition = new Vector2(70f, 80f);
                screen.transform.localScale = Vector3.one * 2;
                if (this._button == null) {
                    this._button = UIHelper.CreateUIButton((screen.transform as RectTransform), "CancelButton", Vector2.zero, Vector2.zero, this.Action, "打开", null) as NoTransitionsButton;
                }
                Logger.Debug($"screem size : {(screen.transform as RectTransform).sizeDelta}");
                Logger.Debug($"button size : {(this._button.transform as RectTransform).sizeDelta}");
                Logger.Debug($"button position : {this._button.transform.position}");
            }
            catch (Exception e) {
                Logger.Error(e);
            }

            this._bot.UpdateRequestUI();
            this.SetButtonColor();

            Logger.Debug("Created request button!");
            Logger.Debug("Start() end");
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            this.isInGame = string.Equals(arg1.name, "GameCore", StringComparison.CurrentCultureIgnoreCase);
        }

        #region Unity message
        protected override void OnDestroy()
        {
            Logger.Debug("OnDestroy");
            this._bot.ChangeButtonColor -= this.SetButtonColor;
            this._bot.RefreshListRequest -= this.RefreshListRequest;
            this._requestFlow.QueueStatusChanged -= this.OnQueueStatusChanged;
            this._requestFlow.PlayProcessEvent -= this.ProcessSongRequest;
            this.DownloadProgress.ProgressChanged -= this.Progress_ProgressChanged;
            SceneManager.activeSceneChanged -= this.SceneManager_activeSceneChanged;
            base.OnDestroy();
        }
        #endregion

        private void OnQueueStatusChanged()
        {
            try {
                var externalComponents = this._button.gameObject.GetComponentsInChildren<ExternalComponents>(true).FirstOrDefault();
                var textMesh = externalComponents.components.FirstOrDefault(x => x as TextMeshProUGUI) as TextMeshProUGUI;

                if (RequestBotConfig.Instance.RequestQueueOpen) {
                    textMesh.text = "打开";
                }
                else {
                    textMesh.text = "关闭";
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private void Progress_ProgressChanged(object sender, double e) => this._requestFlow.ChangeProgressText(e);

        private void RefreshListRequest(bool obj)
        {
            if (this.isInGame) {
                return;
            }
            this._requestFlow.RefreshSongList(obj);
        }

        private async void ProcessSongRequest(SongRequest request, bool fromHistory = false)
        {
            await _downloadSemaphore.WaitAsync();
            try {
                if ((RequestManager.RequestSongs.Any() && !fromHistory) || (RequestManager.HistorySongs.Any() && fromHistory)) {
                    if (!fromHistory) {
                        Logger.Debug("Set status to request");
                        this._bot.SetRequestStatus(request, RequestStatus.Played);
                        this._bot.DequeueRequest(request);
                    }

                    if (request == null) {
                        Logger.Debug("Can't process a null request! Aborting!");
                        return;
                    }
                    else
                        Logger.Debug($"Processing song request {request._song["songName"].Value}");
                    var songName = request._song["songName"].Value;
                    var songIndex = Regex.Replace($"{request._song["id"].Value} ({request._song["songName"].Value} - {request._song["levelAuthor"].Value})", "[\\\\:*/?\"<>|]", "_");
                    songIndex = this.Normalize.RemoveDirectorySymbols(ref songIndex); // Remove invalid characters.

                    var currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data\\CustomLevels", songIndex);
                    var songHash = request._song["hash"].Value.ToUpper();

                    if (Loader.GetLevelByHash(songHash) == null) {
                        Utility.EmptyDirectory(".requestcache", false);

                        if (Directory.Exists(currentSongDirectory)) {
                            Utility.EmptyDirectory(currentSongDirectory, true);
                            Logger.Debug($"Deleting {currentSongDirectory}");
                        }
                        var localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{request._song["id"].Value}.zip");
#if UNRELEASED
                    // Direct download hack
                    var ext = Path.GetExtension(request.song["coverURL"].Value);
                    var k = request.song["coverURL"].Value.Replace(ext, ".zip");

                    var songZip = await Plugin.WebClient.DownloadSong($"https://beatsaver.com{k}", System.Threading.CancellationToken.None);
#endif
                        var result = await WebClient.DownloadSong($"https://beatsaver.com{request._song["downloadURL"].Value}", System.Threading.CancellationToken.None, this.DownloadProgress);
                        if (result == null) {
                            this.ChatManager.QueueChatMessage("BeatSaver is down now.");
                        }
                        using (var zipStream = new MemoryStream(result))
                        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
                            try {
                                // open zip archive from memory stream
                                archive.ExtractToDirectory(currentSongDirectory);
                            }
                            catch (Exception e) {
                                Logger.Debug($"Unable to extract ZIP! Exception: {e}");
                                return;
                            }
                            zipStream.Close();
                        }
                        Dispatcher.RunCoroutine(this.WaitForRefreshAndSchroll(request));
#if UNRELEASED
                        //if (!request.song.IsNull) // Experimental!
                        //{
                        //TwitchWebSocketClient.SendCommand("/marker "+ _textFactory.Create().AddUser(ref request.requestor).AddSong(request.song).Parse(NextSonglink.ToString()));
                        //}
#endif
                    }
                    else {
                        Logger.Debug($"Song {songName} already exists!");
                        Dispatcher.RunOnMainThread(() => this.BackButtonPressed());
                        Dispatcher.RunCoroutine(this.SongListUtils.ScrollToLevel(songHash, () =>
                        {
                            this._bot.UpdateRequestUI();
                        }));
                        if (!request._song.IsNull) {
                            // Display next song message
                            this._textFactory.Create().AddUser(request._requestor).AddSong(request._song).QueueMessage(StringFormat.NextSonglink.ToString());
                        }
                    }
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                _downloadSemaphore.Release();
            }
        }

        private IEnumerator WaitForRefreshAndSchroll(SongRequest request)
        {
            yield return null;
            if (this.isInGame) {
                // ダウンロード中に1曲やるかーって人向けの処理
                while (true) {
                    yield return new WaitWhile(() => this.isInGame);
                    yield return new WaitForSeconds(4.0f);
                    if (!this.isInGame) {
                        break;
                    }
                    yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
                    Loader.Instance.RefreshSongs(false);
                }
                ((IProgress<double>)this.DownloadProgress).Report(0d);
            }
            else {
                yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
                Loader.Instance.RefreshSongs(false);
                yield return new WaitWhile(() => !Loader.AreSongsLoaded && Loader.AreSongsLoading);
                Utility.EmptyDirectory(".requestcache", true);
                
                Dispatcher.RunOnMainThread(() => this.BackButtonPressed());
                Dispatcher.RunCoroutine(this.SongListUtils.ScrollToLevel(request._song["hash"].Value.ToUpper(), () =>
                {
                    this._bot.UpdateRequestUI();
                }));

                ((IProgress<double>)this.DownloadProgress).Report(0d);
                if (!request._song.IsNull) {
                    // Display next song message
                    this._textFactory.Create().AddUser(request._requestor).AddSong(request._song).QueueMessage(StringFormat.NextSonglink.ToString());
                }
            }
        }

        private IEnumerator ChangeButtonColor()
        {
            if (this.isChangeing) {
                yield break;
            }
            this.isChangeing = true;
            while (this.isChangeing) {
                var red = UnityEngine.Random.Range(0f, 1f);
                var green = UnityEngine.Random.Range(0f, 1f);
                var blue = UnityEngine.Random.Range(0f, 1f);
                var color = new Color(red, green, blue);
                this._button.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Underline").color = color;
                yield return this.waitForSeconds;
            }
        }


    }
}
