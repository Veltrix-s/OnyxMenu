using HarmonyLib;

namespace Nocturne;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class NocturneColorCmd
{
    private static readonly string[] Ru =
    {
        "Красный", "Синий", "Зелёный", "Розовый", "Оранжевый", "Жёлтый", "Чёрный", "Белый", "Фиолетовый",
        "Коричневый", "Циан", "Лайм", "Бордовый", "Роза", "Банан", "Серый", "Тан", "Коралл",
    };
    private static readonly string[] En =
    {
        "Red", "Blue", "Green", "Pink", "Orange", "Yellow", "Black", "White", "Purple",
        "Brown", "Cyan", "Lime", "Maroon", "Rose", "Banana", "Gray", "Tan", "Coral",
    };

    public static void Postfix(PlayerControl sourcePlayer, string chatText)
    {
        try
        {
            if (sourcePlayer == null || string.IsNullOrEmpty(chatText)) return;

            string t = chatText.Trim();
            int sp = t.IndexOf(' ');
            string cmd = (sp < 0 ? t : t.Substring(0, sp)).ToLowerInvariant();

            if (cmd == "/help")
            {
                bool amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
                bool isLocal = sourcePlayer == PlayerControl.LocalPlayer;
                NocturneCommands.QueueHelp(amHost, amHost && isLocal, isLocal);
                return;
            }

            if (sourcePlayer == PlayerControl.LocalPlayer && NocturneCommands.TryHostSelf(cmd)) return;

            if (NocturneConfig.ColorCmd == null || !NocturneConfig.ColorCmd.Value) return;
            if (cmd != "/c" && cmd != "/color" && cmd != "/цвет" && cmd != "/с") return;

            int id = ColorId(sp < 0 ? "" : t.Substring(sp + 1).Trim().ToLowerInvariant());
            if (id < 0) return;

            bool host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            if (host)
                sourcePlayer.RpcSetColor((byte)id);
            else if (sourcePlayer == PlayerControl.LocalPlayer)
                PlayerControl.LocalPlayer.CmdCheckColor((byte)id);

            if (NocturneConfig.ColorCmdNotify != null && NocturneConfig.ColorCmdNotify.Value)
            {
                string who = sourcePlayer.Data != null ? sourcePlayer.Data.PlayerName : "?";
                NocturneToast.Push(NocturneText.T("Цвет", "Color"), who + " → " + NocturneText.T(Ru[id], En[id]), 2.5f, NocturneNotifyKind.Info);
            }
        }
        catch { }
    }

    internal static int ColorId(string a)
    {
        if (string.IsNullOrEmpty(a)) return -1;
        if (int.TryParse(a, out int n) && n >= 0 && n <= 17) return n;
        switch (a)
        {
            case "red": case "красный": case "крас": case "кр": return 0;
            case "blue": case "синий": case "син": return 1;
            case "green": case "зелёный": case "зеленый": case "зел": return 2;
            case "pink": case "розовый": case "роз": return 3;
            case "orange": case "оранжевый": case "оранж": case "апельсин": return 4;
            case "yellow": case "жёлтый": case "желтый": case "желт": return 5;
            case "black": case "чёрный": case "черный": case "черн": return 6;
            case "white": case "белый": case "бел": return 7;
            case "purple": case "фиолетовый": case "фиолет": case "фиол": return 8;
            case "brown": case "коричневый": case "коричн": case "корич": return 9;
            case "cyan": case "циан": case "голубой": case "бирюза": case "бирюзовый": return 10;
            case "lime": case "лайм": case "салатовый": case "салат": return 11;
            case "maroon": case "бордовый": case "бордо": case "бордов": return 12;
            case "rose": case "роза": return 13;
            case "banana": case "банан": return 14;
            case "gray": case "grey": case "серый": case "сер": return 15;
            case "tan": case "тан": case "бежевый": case "беж": return 16;
            case "coral": case "коралл": case "коралловый": return 17;
            default: return -1;
        }
    }
}
