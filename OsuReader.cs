using System;
using System.Collections.Generic;
using System.IO;

namespace OsuPlaylistCreator
{
    internal class OsuReader
    {
        private readonly Music MusicItem;
        private readonly IEnumerable<string> FileString;

        public OsuReader(string filePath)
        {
            FileString = File.ReadLines(filePath);
            MusicItem = new Music
            {
                AudioFileName = GetProperty("AudioFilename"),
                Title = GetProperty("Title"),
                Artist = GetProperty("Artist")
            };
        }

        public Music GetMusic() => MusicItem;

        private string GetProperty(string propertyName)
        {
            foreach (string line in FileString)
            {
                string[] lineSplit = line.Split(new char[] { ':' }, 2);
                if (!lineSplit[0].Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                return lineSplit[1].Trim();
            }

            return null;
        }
    }

    internal class Music
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string AudioFileName { get; set; }
    }
}
