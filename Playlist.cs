using NAudio.Wave;
using OsuPlaylistCreator.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OsuPlaylistCreator
{
    internal class Playlist
    {
        private readonly StringBuilder sb = new StringBuilder("#EXTM3U\n");
        private readonly HashSet<string> MusicHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> MusicTitle = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int Count { get; private set; }

        public bool AddToPlaylist(string name, string filePath, out string errReason)
        {
            if (Settings.Default.CheckHash && !MusicHash.Add(BitConverter.ToString(MD5.Create().ComputeHash(File.ReadAllBytes(filePath)))))
            {
                errReason = "Music hash already exists";
                return false;
            }

            if (Settings.Default.CheckName && !MusicTitle.Add(ToAlpha(name)))
            {
                errReason = "Music title already exists";
                return false;
            }

            double musicDuration = -1;
            try
            {
                MediaFoundationReader reader = new MediaFoundationReader(filePath);
                if (Settings.Default.MinimumDuration != -1 && reader.TotalTime.TotalSeconds < Settings.Default.MinimumDuration)
                {
                    errReason = $"Music duration is less than {Settings.Default.MinimumDuration} seconds";
                    return false;
                }

                musicDuration = reader.TotalTime.TotalSeconds;
            }
            catch (COMException)
            {
                if (Settings.Default.ExcludeUnparsableAudios)
                {
                    errReason = $"Unable to parse audio";
                    return false;
                }
            }

            //Create entry
            lock(sb)
            {
                sb.AppendLine($"#EXTINF:{musicDuration}, {name}");
                sb.AppendLine(filePath);
            }

            Count++;
            errReason = null;
            return true;
        }

        public string Get() => sb.ToString();

        //Some music titles might use obscure symbols. This method theoretically prevents multiple song entries with a same title despite having weird title symbols.
        private string ToAlpha(string str) => new string(Array.FindAll(str.ToCharArray(), c => char.IsLetterOrDigit(c)));
    }
}
