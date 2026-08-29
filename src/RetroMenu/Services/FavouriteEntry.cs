using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RetroMenu.Services
{
    /// <summary>
    /// One line in the favourites group at the top of the left column: either a
    /// program, or a folder holding several. Windows 11 lets pinned entries be
    /// grouped that way; here the group opens as a cascading menu instead of a
    /// grid, which is what the rest of the menu looks like.
    /// </summary>
    public sealed class FavouriteEntry
    {
        /// <summary>Set when this line is a single program.</summary>
        public string Id { get; set; }

        /// <summary>Set when this line is a folder.</summary>
        public string Folder { get; set; }

        /// <summary>What the folder holds, in the order it should appear.</summary>
        public List<string> Items { get; set; } = new List<string>();

        [JsonIgnore]
        public bool IsFolder => !string.IsNullOrEmpty(Folder);
    }
}
