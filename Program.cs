using OsuPlaylistCreator.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OsuPlaylistCreator
{
    internal static class Program
    {
        private static Thread[] ParsingThreads;
        private static int ParseCount;
        private static string[] MusicDirectories;
        private readonly static Playlist Playlist = new Playlist();

        static void Main(string[] args)
        {
            if ((args.Length != 0 && args[0].Equals("r")) || string.IsNullOrWhiteSpace(Settings.Default.Path) || !Directory.Exists(Settings.Default.Path))
            {
                Console.Write("Music Path: ");
                Settings.Default.Path = Console.ReadLine();
                Settings.Default.Save();
            }

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
                    string musicTitle = $"{m.Artist} - {m.Title}";

                    if (!Playlist.AddToPlaylist(musicTitle, Path.Combine(MusicDirectories[startIndex + x], m.AudioFileName), out string errReason))
                        t.Output($"Failed to add {musicTitle}. {errReason}");
                    else
                        t.Output($"{musicTitle} added");
                }
            }
        }

        public static void Output(this Thread t, object output) => Console.WriteLine($"[{t.Name}] {output}");
    }
}
