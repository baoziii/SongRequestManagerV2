﻿using ChatCore.Interfaces;
using ChatCore.Models.Twitch;
using ChatCore.Models.BiliBili;
using SongRequestManagerV2.Configuration;
using SongRequestManagerV2.Interfaces;
using SongRequestManagerV2.SimpleJSON;
using SongRequestManagerV2.Statics;
using System;
using System.IO;
using System.Linq;

namespace SongRequestManagerV2.Utils
{
    public static class Utility
    {
        public static void EmptyDirectory(string directory, bool delete = true)
        {
            if (Directory.Exists(directory)) {
                var directoryInfo = new DirectoryInfo(directory);
                foreach (var file in directoryInfo.GetFiles()) {
                    file.Delete();
                }

                foreach (var subDirectory in directoryInfo.GetDirectories()) {
                    subDirectory.Delete(true);
                }

                if (delete) {
                    Directory.Delete(directory);
                }
            }
        }

        public static TimeSpan GetFileAgeDifference(string filename)
        {
            var lastModified = File.GetLastWriteTime(filename);
            return DateTime.Now - lastModified;
        }

        public static bool HasRights(ISRMCommand botcmd, IChatUser user, CmdFlags flags)
        {
            if (flags.HasFlag(CmdFlags.Local)) {
                return true;
            }

            if (botcmd.Flags.HasFlag(CmdFlags.Disabled)) {
                return false;
            }

            if (botcmd.Flags.HasFlag(CmdFlags.Everyone)) {
                return true; // Not sure if this is the best approach actually, not worth thinking about right now
            }

            if (user.IsModerator & RequestBotConfig.Instance.ModFullRights) {
                return true;
            }

            if (user.IsBroadcaster & botcmd.Flags.HasFlag(CmdFlags.Broadcaster)) {
                return true;
            }

            if (user.IsModerator & botcmd.Flags.HasFlag(CmdFlags.Mod)) {
                return true;
            }
            
            if (user is TwitchUser twitchUser && twitchUser.IsSubscriber & botcmd.Flags.HasFlag(CmdFlags.Sub)) {
                return true;
            }
            
            if (user is TwitchUser twitchUser1 && twitchUser1.IsVip & botcmd.Flags.HasFlag(CmdFlags.VIP)) {
                return true;
            }

            return false;
        }

        public static string GetStarRating(JSONObject song, bool mode = true)
        {
            if (!mode) {
                return "";
            }

            var version = song["versions"].AsArray.Children.FirstOrDefault(x => x["state"].Value == MapStatus.Published.ToString());
            if (version == null) {
                return "";
            }
            var maxstar = 0f;
            foreach (var diff in version["diffs"].AsArray.Children) {
                if (maxstar < diff["stars"].AsFloat) {
                    maxstar = diff["stars"].AsFloat;
                }
            }
            var stars = "******";
            var rating = maxstar;
            if (rating < 0 || rating > 100) {
                rating = 0;
            }

            var starrating = stars.Substring(0, (int)(rating / 17)); // 17 is used to produce a 5 star rating from 80ish to 100.
            return starrating;
        }

        public static string GetRating(JSONObject song, bool mode = true)
        {
            if (!mode) {
                return "";
            }

            var rating = song["stats"]["score"].AsFloat * 100f;
            if (rating == 0) {
                return "";
            }

            return $"{rating:0.0}%";
        }

        public static bool IsAprilFool()
        {
#if DEBUG
            return RequestBotConfig.Instance.EnableAprilFool;
#else
            return RequestBotConfig.Instance.EnableAprilFool && DateTime.Now.Month == 4 && DateTime.Now.Day == 1;
#endif
        }
    }
}
