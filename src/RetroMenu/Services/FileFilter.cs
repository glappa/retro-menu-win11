using System;

namespace RetroMenu.Services
{
    /// <summary>
    /// What the advanced search narrows a file query down to. Every field left
    /// empty means "anything", so a fresh filter behaves like the plain search.
    /// </summary>
    public sealed class FileFilter
    {
        /// <summary>
        /// A Windows Search kind — document, picture, music, video, program — or
        /// "folder", which the index answers through the item type instead.
        /// Null searches everything.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>The folder to stay inside, or null for the whole index.</summary>
        public string Folder { get; set; }

        /// <summary>How far back the file may have been changed, or null for ever.</summary>
        public TimeSpan? Within { get; set; }

        public bool IsEmpty =>
            string.IsNullOrEmpty(Kind) && string.IsNullOrWhiteSpace(Folder) && !Within.HasValue;
    }
}
