using System;

namespace EnixBrowser.Models;

public sealed class Bookmark
{
    public string Title { get; set; }
    public string Url { get; set; }
    public bool IsPinnedToBar { get; set; }
    public DateTime CreatedAt { get; set; }

    public Bookmark()
    {
        Title = string.Empty;
        Url = string.Empty;
        IsPinnedToBar = true;
        CreatedAt = DateTime.UtcNow;
    }

    public Bookmark(string title, string url, bool isPinnedToBar = true)
    {
        Title = title;
        Url = url;
        IsPinnedToBar = isPinnedToBar;
        CreatedAt = DateTime.UtcNow;
    }

    public override string ToString() => $"{Title} ({Url})";
}