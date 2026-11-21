using System;
using System.Collections.Generic;

namespace ArisenStudio.Forms.Dashboard
{
    /// <summary>
    /// Console family a dashboard favourite belongs to. The dashboard keeps its own small enum so that it does not
    /// depend on the shell's platform type; the shell maps between the two.
    /// </summary>
    public enum DashboardPlatform
    {
        PS3,
        PS4,
        Xbox360
    }

    /// <summary>One "Our Favorite Games" entry (ledger MW1-095, MW1-096).</summary>
    public sealed class FavoriteGameInfo
    {
        public DashboardPlatform Platform { get; set; }

        public string CategoryId { get; set; }

        /// <summary>Display title of the category (the old code looked it up with Database.CategoriesData.GetCategoryById).</summary>
        public string CategoryTitle { get; set; }

        /// <summary>
        /// Number of library items in the category: game mods for PS3 and Xbox 360, homebrew for PS4.
        /// The tile subtitle reads "N Total Game Mods" or "N Total Homebrew".
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>Cover image address. May be empty. Loaded through <see cref="IDashboardDataSource.LoadImageAsync"/>.</summary>
        public string ImageUrl { get; set; }
    }

    /// <summary>One "Our Favorite Mods" entry (ledger MW1-098, MW1-099).</summary>
    public sealed class FavoriteModInfo
    {
        public DashboardPlatform Platform { get; set; }

        public string CategoryId { get; set; }

        public string CategoryTitle { get; set; }

        public int ModId { get; set; }

        public string ModName { get; set; }
    }

    /// <summary>One announcement (ledger MW1-105).</summary>
    public sealed class AnnouncementInfo
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        public DateTime DateStart { get; set; }

        public DateTime DateEnd { get; set; }
    }

    /// <summary>One news feed entry (ledger MW1-106).</summary>
    public sealed class NewsEntry
    {
        public string Title { get; set; }

        /// <summary>Feed description. May contain HTML; the news item turns it into plain text.</summary>
        public string Description { get; set; }

        public DateTime Published { get; set; }
    }

    /// <summary>One release for the change log card (ledger MW1-109, MW1-113, MW1-114). Index 0 is the newest.</summary>
    public sealed class ReleaseInfo
    {
        public string Name { get; set; }

        public string Body { get; set; }

        public DateTime PublishedAt { get; set; }
    }

    /// <summary>One counted line in a statistics block, shown as "1,234 Game Mods".</summary>
    public sealed class StatLine
    {
        public StatLine(int count, string label)
        {
            Count = count;
            Label = label;
        }

        public int Count { get; private set; }

        public string Label { get; private set; }
    }

    /// <summary>A titled block of statistics lines (for example "PlayStation 3").</summary>
    public sealed class StatGroup
    {
        public StatGroup(string title)
        {
            Title = title;
            Lines = new List<StatLine>();
        }

        public string Title { get; private set; }

        public List<StatLine> Lines { get; private set; }
    }

    /// <summary>Everything the statistics card shows (ledger MW1-107).</summary>
    public sealed class DashboardStats
    {
        public DashboardStats()
        {
            Groups = new List<StatGroup>();
        }

        public long TotalDownloads { get; set; }

        /// <summary>Database last-updated time, ideally UTC. Null hides the "Last Updated" line.</summary>
        public DateTime? LastUpdated { get; set; }

        public List<StatGroup> Groups { get; private set; }
    }

    /// <summary>Raised when a favourite game tile is clicked. The shell opens the matching mods or homebrew page (MW1-097).</summary>
    public sealed class FavoriteGameEventArgs : EventArgs
    {
        public FavoriteGameEventArgs(FavoriteGameInfo game)
        {
            Game = game;
        }

        public FavoriteGameInfo Game { get; private set; }
    }

    /// <summary>Raised when a favourite mod tile is clicked. The shell opens the mod details dialog (MW1-100, MW3-113).</summary>
    public sealed class FavoriteModEventArgs : EventArgs
    {
        public FavoriteModEventArgs(FavoriteModInfo mod)
        {
            Mod = mod;
        }

        public FavoriteModInfo Mod { get; private set; }
    }

    /// <summary>Raised after an announcement was dismissed and the data source was told about it.</summary>
    public sealed class AnnouncementDismissedEventArgs : EventArgs
    {
        public AnnouncementDismissedEventArgs(int id)
        {
            Id = id;
        }

        public int Id { get; private set; }
    }

    /// <summary>A message the shell may show in its status bar (for example a failed news load).</summary>
    public sealed class DashboardStatusEventArgs : EventArgs
    {
        public DashboardStatusEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }

        public string Message { get; private set; }

        public Exception Exception { get; private set; }
    }
}
