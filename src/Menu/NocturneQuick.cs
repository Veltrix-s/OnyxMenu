using System.Collections.Generic;
using BepInEx.Configuration;

namespace Nocturne;

internal sealed class QuickItem
{
    internal readonly string Id, Ru, En;
    internal readonly ConfigEntry<bool> Cfg;
    internal QuickItem(string id, string ru, string en, ConfigEntry<bool> cfg) { Id = id; Ru = ru; En = en; Cfg = cfg; }
    internal string Label => NocturneText.T(Ru, En);
}

internal static class NocturneQuick
{
    private const int MaxFavs = 10;
    private static QuickItem[] _items;

    internal static QuickItem[] Items => _items ??= Build();

    internal static QuickItem ById(string id)
    {
        foreach (QuickItem it in Items) if (it.Id == id) return it;
        return null;
    }

    internal static bool IsFav(string id) => FavIds().Contains(id);

    private static readonly List<string> _favs = new List<string>();
    private static string _favsRaw = null;

    internal static List<string> FavIds()
    {
        string s = NocturneConfig.RadialFavorites.Value;
        if (s == _favsRaw) return _favs;

        _favsRaw = s;
        _favs.Clear();
        if (string.IsNullOrEmpty(s)) return _favs;
        foreach (string part in s.Split(','))
        {
            string p = part.Trim();
            if (p.Length > 0 && ById(p) != null && !_favs.Contains(p)) _favs.Add(p);
        }
        return _favs;
    }

    internal static void ToggleFav(string id)
    {
        var list = new List<string>(FavIds());
        if (list.Contains(id)) list.Remove(id);
        else if (list.Count < MaxFavs) list.Add(id);
        NocturneConfig.RadialFavorites.Value = string.Join(",", list);
    }

    private static QuickItem[] Build() => new[]
    {
        new QuickItem("god", "Бессмертие", "God Mode", NocturneConfig.GodMode),
        new QuickItem("nukegame", "Спам-собрания", "Spam meetings", NocturneConfig.NukeGame),
        new QuickItem("invisible", "Невидимость", "Invisibility", NocturneConfig.Invisible),
        new QuickItem("fakescan", "Фейк-скан", "Fake scan", NocturneConfig.FakeScan),
        new QuickItem("fakecams", "Камеры заняты", "Cameras in use", NocturneConfig.FakeCams),
        new QuickItem("mirage", "Мираж", "Mirage", NocturneConfig.LagComp),
        new QuickItem("speed", "Своя скорость", "Custom speed", NocturneConfig.SpeedMod),
        new QuickItem("invert", "Инверт управления", "Invert controls", NocturneConfig.InvertControls),
        new QuickItem("noclip", "Ноклип", "No-clip", NocturneConfig.VisualNoClip),
        new QuickItem("freecam", "Свободная камера", "Free camera", NocturneConfig.VisualFreeCamera),
        new QuickItem("zoom", "Зум камеры", "Camera zoom", NocturneConfig.VisualCameraZoom),
        new QuickItem("esp", "ESP-боксы", "ESP boxes", NocturneConfig.EspBoxes),
        new QuickItem("neon", "Неон-обводка", "Neon outline", NocturneConfig.NeonOutline),
        new QuickItem("radar", "Радар", "Radar", NocturneConfig.Radar),
        new QuickItem("tracers", "Трассеры", "Tracers", NocturneConfig.Tracers),
        new QuickItem("tracebody", "Трассеры к телам", "Body tracers", NocturneConfig.TracerBodies),
        new QuickItem("taskarrows", "Стрелки к таскам", "Task arrows", NocturneConfig.TaskArrows),
        new QuickItem("killcd", "Кулдаун киллов", "Kill cooldown ESP", NocturneConfig.KillTimers),
        new QuickItem("seevents", "Видеть венты", "See vents", NocturneConfig.SeeVents),
        new QuickItem("seeghosts", "Видеть призраков", "See ghosts", NocturneConfig.SeeGhosts),
        new QuickItem("wallhack", "Wallhack", "Wallhack", NocturneConfig.Wallhack),
        new QuickItem("roles", "Показывать роли", "Reveal roles", NocturneConfig.RevealRoles),
        new QuickItem("unmask", "Раскрыть Оборотня", "Unmask shapeshifter", NocturneConfig.UnmaskShapeshifter),
        new QuickItem("votes", "Голоса на собрании", "Meeting votes", NocturneConfig.RevealVotes),
        new QuickItem("anonvotes", "Раскрыть анонимные", "De-anon votes", NocturneConfig.RevealAnonVotes),
        new QuickItem("infonames", "Инфо над игроками", "Info over players", NocturneConfig.VisualPlayerInfoNames),
        new QuickItem("skipshhh", "Скип Shhh", "Skip Shhh", NocturneConfig.SkipShhh),
        new QuickItem("ghoststart", "Призрак после старта", "Ghost after start", NocturneConfig.GhostAfterStart),
        new QuickItem("mouseteleport", "Телепорт по ПКМ", "Teleport on RMB", NocturneConfig.MouseTeleport),
        new QuickItem("mouseselect", "Выбор мышью", "Mouse select", NocturneConfig.MouseSelect),
        new QuickItem("cosmetics", "Разблок косметики", "Unlock cosmetics", NocturneConfig.FreeCosmetics),
        new QuickItem("hidecos", "Скрыть косметику в матче", "Hide cosmetics in match", NocturneConfig.HideCosmeticsInMatch),
        new QuickItem("modstamp", "Скрыть MOD-штамп", "Hide MOD stamp", NocturneConfig.HideModStamp),
        new QuickItem("namecolor", "Цветной ник", "Colored name", NocturneConfig.NameColor),
        new QuickItem("autohost", "Автохост", "Auto-host", NocturneConfig.AutoHostEnabled),
        new QuickItem("dummies", "Манекены", "Dummies", NocturneConfig.DummyEnabled),
        new QuickItem("doors", "Двери открыты", "Doors kept open", NocturneConfig.DoorKeepOpen),
        new QuickItem("sablights", "Держать свет выкл", "Keep lights off", NocturneConfig.SabAutoLights),
        new QuickItem("sabspam", "Спам саботажа", "Spam sabotage", NocturneConfig.SabSpamReactor),
        new QuickItem("nowin", "Без условий победы", "No win conditions", NocturneConfig.NoWinConditions),
        new QuickItem("spoofplatform", "Спуф платформы", "Spoof platform", NocturneConfig.SpoofPlatformEnabled),
        new QuickItem("spooflevel", "Спуф уровня", "Spoof level", NocturneConfig.SpoofLevelEnabled),
        new QuickItem("telemetry", "Блок телеметрии", "Block telemetry", NocturneConfig.BlockTelemetry),
        new QuickItem("rpcguard", "Античит RPC", "RPC anticheat", NocturneConfig.RpcGuard),
        new QuickItem("vkprotect", "Блок войткиков", "Block votekicks", NocturneConfig.VoteKickProtect),
        new QuickItem("betterchat", "Улучшенный чат", "Better chat", NocturneConfig.BetterChat),
        new QuickItem("ghostchat", "Чат мёртвых", "Ghost chat", NocturneConfig.GhostChat),
        new QuickItem("chatlog", "Лог чата", "Chat log", NocturneConfig.ChatLog),
        new QuickItem("lobbybar", "Лобби-бар", "Lobby bar", NocturneConfig.LobbyBar),
    };
}
