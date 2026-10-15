using System;

namespace Nocturne;

internal enum ServiceGlyph
{
    Note, Arcs, Play, Bars, Mark
}

internal sealed class MediaService
{
    internal string Name;
    internal string NameRu;
    internal string Search;
    internal string Library;
    internal string App;
    internal string AppLibrary;
    internal string AppProc;
    internal uint Tint;
    internal uint Ink;
    internal ServiceGlyph Glyph;
    internal string Mark;
    internal bool Flat;
    internal string[] Keys;

    internal string Label => NameRu != null && NocturneText.IsRussian ? NameRu : Name;
}

internal static class MediaServices
{
    internal static readonly MediaService Spotify = new MediaService
    {
        Name = "Spotify",
        Search = "https://open.spotify.com/search/{0}",
        Library = "https://open.spotify.com/collection/playlists",
        App = "spotify:search:{0}",
        AppLibrary = "spotify:collection:playlists",
        AppProc = "Spotify",
        Tint = 0x1ED760,
        Ink = 0x07140B,
        Glyph = ServiceGlyph.Arcs,
        Keys = new[] { "Spotify" },
    };

    private static readonly MediaService[] Table =
    {
        new MediaService
        {
            Name = "YouTube Music",
            Search = "https://music.youtube.com/search?q={0}",
            Library = "https://music.youtube.com/library/playlists",
            Tint = 0xFF1F3D,
            Ink = 0xFFFFFF,
            Glyph = ServiceGlyph.Play,
            Keys = new[] { "YouTube Music" },
        },
        new MediaService
        {
            Name = "YouTube",
            Search = "https://www.youtube.com/results?search_query={0}",
            Library = "https://www.youtube.com/feed/playlists",
            Tint = 0xFF0000,
            Ink = 0xFFFFFF,
            Glyph = ServiceGlyph.Play,
            Flat = true,
            Keys = new[] { "YouTube" },
        },
        Spotify,
        new MediaService
        {
            Name = "SoundCloud",
            Search = "https://soundcloud.com/search?q={0}",
            Library = "https://soundcloud.com/you/sets",
            Tint = 0xFF5500,
            Ink = 0xFFFFFF,
            Glyph = ServiceGlyph.Bars,
            Keys = new[] { "SoundCloud" },
        },
        new MediaService
        {
            Name = "Yandex Music",
            NameRu = "Яндекс Музыка",
            Search = "https://music.yandex.ru/search?text={0}",
            Library = "https://music.yandex.ru/collection",
            Tint = 0xFFD21E,
            Ink = 0x14110A,
            Glyph = ServiceGlyph.Note,
            Keys = new[] { "Яндекс Музык", "Яндекс.Музык", "Yandex Music" },
        },
        new MediaService
        {
            Name = "VK Music",
            NameRu = "VK Музыка",
            Search = "https://vk.com/audio?q={0}",
            Library = "https://vk.com/audio",
            Tint = 0x2787F5,
            Ink = 0xFFFFFF,
            Glyph = ServiceGlyph.Mark,
            Mark = "VK",
            Keys = new[] { "VK Музык", "VK Music", "ВКонтакте", "VKontakte", "| VK", "- VK", "– VK" },
        },
        new MediaService
        {
            Name = "Apple Music",
            Search = "https://music.apple.com/search?term={0}",
            Library = "https://music.apple.com/library/playlists",
            Tint = 0xFA243C,
            Ink = 0xFFFFFF,
            Glyph = ServiceGlyph.Note,
            Keys = new[] { "Apple Music" },
        },
        new MediaService
        {
            Name = "Deezer",
            Search = "https://www.deezer.com/search/{0}",
            Tint = 0xA238FF,
            Ink = 0xFFFFFF,
            Glyph = ServiceGlyph.Bars,
            Keys = new[] { "Deezer" },
        },
    };

    internal static MediaService FromTitle(string title, bool ours)
    {
        for (int i = 0; i < Table.Length; i++)
        {
            string[] keys = Table[i].Keys;
            for (int k = 0; k < keys.Length; k++)
            {
                if (title.IndexOf(keys[k], StringComparison.OrdinalIgnoreCase) >= 0)
                    return Table[i];
            }
        }
        return ours && title.IndexOf(" • ", StringComparison.Ordinal) >= 0 ? Spotify : null;
    }

    internal static MediaService FromApp(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        string low = id.ToLowerInvariant();
        if (low.Contains("spotify"))
            return Spotify;
        if (low.Contains("applemusic"))
            return Find("Apple Music");
        if (low.Contains("yandex.music") || low.Contains("yandexmusic"))
            return Find("Yandex Music");
        if (low.Contains("deezer"))
            return Find("Deezer");
        return null;
    }

    internal static string SearchUrl(MediaService s, string query) => string.Format(s.Search, Uri.EscapeDataString(query));

    internal static string AppUrl(MediaService s, string query) => string.Format(s.App, Uri.EscapeDataString(query));

    private static MediaService Find(string name)
    {
        for (int i = 0; i < Table.Length; i++)
        {
            if (Table[i].Name == name)
                return Table[i];
        }
        return null;
    }
}
