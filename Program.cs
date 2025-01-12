using Microsoft.Win32;
using OsuPlaylistCreator.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace OsuPlaylistCreator
{
    internal static class Program
    {
        private static Thread[] ParsingThreads;
        private static int ParseCount;
        private static string[] MusicDirectories;
        private readonly static Playlist Playlist = new Playlist();
        private readonly static HashSet<int> ExcludedIDs = new HashSet<int>();

        static void Main(string[] args)
        {
            //Parse excluded IDs
            if (Settings.Default.UseExcludeFile && File.Exists("exclude.txt"))
            {
                string[] fileLines = File.ReadAllLines("exclude.txt");

                foreach(string fileLine in fileLines)
                {
                    if (fileLine[0] == '#')
                        continue;

                    bool isValidID = int.TryParse(fileLine.Trim(), out int id);
                    if (isValidID)
                        ExcludedIDs.Add(id);
                }

                Console.WriteLine("Excluded {0} IDs", ExcludedIDs.Count);
            }

            ResolveOsuPath(args);
            try
            {
                MusicDirectories = Directory.GetDirectories(Settings.Default.Path);
            }
            catch (ArgumentException)
            {
                ShowError("Invalid path.");
                return;
            }
            catch (DirectoryNotFoundException)
            {
                ShowError("Directory not found. Are you using a correct path?");
                return;
            }

            if (MusicDirectories.Length > 0)
            {
                Console.WriteLine($"Found {MusicDirectories.Length} directories, Parsing...");
                StartOperation();
            }
            else
            {
                ShowError("No directory/ies found. Are you using a correct path?");
                return;
            }

            if (Settings.Default.ExitTime > 0)
                Thread.Sleep(Settings.Default.ExitTime);
            else
                Console.ReadKey(true);
        }

        private static void ResolveOsuPath(string[] args)
        {
            //get install path from registry key inside the "Uninstall" path
            string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            RegistryKey key = null;
            if (Settings.Default.ResolveOsuPath)
                key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(registryKey); //osu is 32-bit exclusive

            if (key != null)
            {
                foreach (RegistryKey subkey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
                {
                    if (subkey.GetValue("DisplayName") is string displayName && displayName.Contains("osu!"))
                    {
                        Console.WriteLine("osu! path found");
                        Settings.Default.Path = Path.Combine(subkey.GetValue("DisplayIcon").ToString(), "..", "Songs");
                        key.Close();
                        return;
                    }
                }

                key.Close();
            }

            if ((args.Length != 0 && args[0].Equals("r")) || string.IsNullOrWhiteSpace(Settings.Default.Path) || !Directory.Exists(Settings.Default.Path))
            {
                Console.Write("Music Path: ");
                Settings.Default.Path = Console.ReadLine();
                Settings.Default.Save();
            }
        }

        private static void ShowError(string message)
        {
            Console.WriteLine(message);
            Settings.Default.Path = null;
            Settings.Default.Save();
            Main(new string[] { "r" });
        }

        private static void StartOperation()
        {
            //Spawn threads
            if (Settings.Default.ThreadCount > MusicDirectories.Length)
                ParsingThreads = new Thread[MusicDirectories.Length];
            else if (Settings.Default.ThreadCount < 1)
                ParsingThreads = new Thread[1];
            else
                ParsingThreads = new Thread[Settings.Default.ThreadCount];

            Console.WriteLine($"Spawning {ParsingThreads.Length} threads...");
            ParseCount = (int)Math.Ceiling((decimal)MusicDirectories.Length / ParsingThreads.Length);
            Console.WriteLine($"Each thread will process {ParseCount} directories");

            for (int x = 0; x < ParsingThreads.Length; x++)
            {
                int startIndex = x * ParseCount;
                ParsingThreads[x] = new Thread(() => ParseMusicFolder(startIndex))
                {
                    Name = $"Thread {x}"
                };

                ParsingThreads[x].Start();
            }

            foreach (Thread t in ParsingThreads)
                t.Join();

            File.WriteAllText("playlist.m3u", Playlist.Get());
            Console.WriteLine($"Process complete. Total music: {Playlist.Count}");
            if (Playlist.Count > 0)
            {
                Console.WriteLine("Launching playlist file...");
                Process.Start("playlist.m3u");
            }

            Console.WriteLine("Will exit...");
        }

        private static void ParseMusicFolder(int startIndex)
        {
            Thread t = Thread.CurrentThread;

            for (int x = 0; x <= ParseCount && startIndex + x < MusicDirectories.Length; x++)
            {
                foreach (string osuFilePath in Directory.GetFiles(MusicDirectories[startIndex + x], "*.osu"))
                {
                    OsuReader osuReader = new OsuReader(osuFilePath);
                    Music m = osuReader.GetMusic();

                    if (ExcludedIDs.Contains(m.BeatmapSetID))
                        continue;

                    string musicTitle = $"{m.Artist} - {m.Title}";

                    if (!Playlist.AddToPlaylist(musicTitle, Path.Combine(MusicDirectories[startIndex + x], m.AudioFileName), out string errReason) && Settings.Default.LogFailedParse)
                        t.Output($"Failed to add {musicTitle}. {errReason}");
                    else
                        t.Output($"{musicTitle} added");
                }
            }
        }

        public static void Output(this Thread t, object output) => Console.WriteLine($"[{t.Name}] {output}");
    }
}
