using UnityEngine;

namespace Nocturne;

internal static class UiInput
{
    private static int _chatFrame = -10;
    private static bool _chatOpen;

    internal static bool Typing => NocturneMenu.Typing || NocturneChatWindow.Typing || Patches.NocturneTextFocus.Any;

    internal static bool ChatOpen
    {
        get
        {
            int frame = Time.frameCount;
            if (_chatFrame != frame)
            {
                _chatFrame = frame;
                HudManager hud = DestroyableSingleton<HudManager>.InstanceExists ? DestroyableSingleton<HudManager>.Instance : null;
                ChatController chat = hud != null ? hud.Chat : null;
                _chatOpen = chat != null && chat.IsOpenOrOpening;
            }
            return _chatOpen;
        }
    }

    internal static bool WorldDown(int button) => Input.GetMouseButtonDown(button) && !Patches.MouseShield.Blocked(button);

    internal static bool WorldHeld(int button) => Input.GetMouseButton(button) && !Patches.MouseShield.Blocked(button);

    internal static float WorldScroll
    {
        get
        {
            float wheel = Input.mouseScrollDelta.y;
            return wheel != 0f && Patches.MouseShield.Over ? 0f : wheel;
        }
    }
}
