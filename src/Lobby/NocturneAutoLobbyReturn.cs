using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;

namespace Nocturne;

public sealed class NocturneAutoLobbyReturn : MonoBehaviour
{
    private const float AutoReturnDelaySeconds = 3f;
    private const float AutoReturnRetrySeconds = 0.4f;
    private const int AutoReturnMaxAttempts = 40;

    private int trackedEndGameId;
    private int exhaustedEndGameId;
    private int attempt;
    private float nextAttemptAt;
    private bool pending;
    private float nextScanAt;

    public void Update()
    {
        if (!ShouldAutoReturn())
        {
            ResetState();
            return;
        }

        if (LobbyBehaviour.Instance != null)
        {
            ResetState();
            return;
        }

        if (Time.unscaledTime < nextScanAt)
        {
            return;
        }

        nextScanAt = Time.unscaledTime + 0.4f;

        EndGameManager manager = Object.FindObjectOfType<EndGameManager>();
        if (manager != null)
        {
            int endGameId = manager.GetInstanceID();
            if (trackedEndGameId != endGameId)
            {
                trackedEndGameId = endGameId;
                exhaustedEndGameId = 0;
                attempt = 0;
                nextAttemptAt = Time.unscaledTime + AutoReturnDelaySeconds;
                pending = true;
            }
        }
        else if (trackedEndGameId == 0)
        {
            return;
        }

        if (!pending || exhaustedEndGameId == trackedEndGameId || Time.unscaledTime < nextAttemptAt)
        {
            return;
        }

        bool acted = false;
        if (manager != null)
        {
            acted = TryInvokeEndGameAction(manager);
            acted = TryClickEndGameButtons(manager) || acted;
        }

        acted = TryClickGlobalReturnButtons() || acted;

        if (LobbyBehaviour.Instance != null)
        {
            ResetState();
            return;
        }

        attempt++;
        if (attempt >= AutoReturnMaxAttempts)
        {
            pending = false;
            exhaustedEndGameId = trackedEndGameId;
            return;
        }

        nextAttemptAt = Time.unscaledTime + AutoReturnRetrySeconds;
    }

    public void OnDisable()
    {
        ResetState();
    }

    private void ResetState()
    {
        trackedEndGameId = 0;
        exhaustedEndGameId = 0;
        attempt = 0;
        nextAttemptAt = 0f;
        pending = false;
        nextScanAt = 0f;
    }

    private static bool ShouldAutoReturn()
    {
        return NocturneConfig.AutoReturnLobbyAfterMatch.Value
            || Patches.NocturneAutoHostService.ShouldReturnAfterMatch;
    }

    private static bool TryInvokeEndGameAction(EndGameManager manager)
    {
        if (manager == null)
        {
            return false;
        }

        string[] methodNames = { "Continue", "NextGame", "PlayAgain" };
        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method = FindMethodNoWarn(manager.GetType(), methodNames[i], Type.EmptyTypes);
            if (method == null)
            {
                continue;
            }

            try
            {
                method.Invoke(manager, null);
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static MethodInfo FindMethodNoWarn(Type type, string name, Type[] parameters)
    {
        if (type == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Type[] signature = parameters ?? Type.EmptyTypes;
        for (Type current = type; current != null; current = current.BaseType)
        {
            MethodInfo method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, signature, null);
            if (method != null)
            {
                return method;
            }
        }

        return null;
    }

    private static bool TryClickEndGameButtons(EndGameManager manager)
    {
        if (manager == null)
        {
            return false;
        }

        Component root = manager;
        if (TryClickPassiveButtons(root.GetComponentsInChildren<PassiveButton>(true), onlyActive: true))
        {
            return true;
        }

        return TryClickUnityButtons(root.GetComponentsInChildren<Button>(true), onlyActive: true);
    }

    private static bool TryClickGlobalReturnButtons()
    {
        if (TryClickPassiveButtons(Object.FindObjectsOfType<PassiveButton>(), onlyActive: true))
        {
            return true;
        }

        return TryClickUnityButtons(Object.FindObjectsOfType<Button>(), onlyActive: true);
    }

    private static bool TryClickPassiveButtons(PassiveButton[] buttons, bool onlyActive)
    {
        if (buttons == null)
        {
            return false;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            PassiveButton button = buttons[i];
            if (button == null)
            {
                continue;
            }

            Component component = button;
            if (onlyActive && (!component.gameObject.activeInHierarchy || !button.isActiveAndEnabled))
            {
                continue;
            }

            if (!IsLobbyReturnButton(button.name, component.GetComponentsInChildren<TMP_Text>(true)))
            {
                continue;
            }

            try
            {
                if ((UnityEvent)(object)button.OnClick == null)
                {
                    continue;
                }

                ((UnityEvent)button.OnClick).Invoke();
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool TryClickUnityButtons(Button[] buttons, bool onlyActive)
    {
        if (buttons == null)
        {
            return false;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            Component component = button;
            if (onlyActive && (!component.gameObject.activeInHierarchy || !button.isActiveAndEnabled || !button.interactable))
            {
                continue;
            }

            if (!IsLobbyReturnButton(button.name, component.GetComponentsInChildren<TMP_Text>(true)))
            {
                continue;
            }

            try
            {
                ((UnityEvent)button.onClick).Invoke();
                return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool IsLobbyReturnButton(string objectName, TMP_Text[] texts)
    {
        string normalizedName = (objectName ?? string.Empty).ToLowerInvariant();
        if (ContainsAny(normalizedName, "exit", "quit", "menu", "back", "leave", "вых", "выйт", "назад"))
        {
            return false;
        }

        if (ContainsAny(normalizedName, "continue", "nextgame", "playagain", "returntolobby", "tolobby", "lobby", "again", "продолж", "занов", "снов", "лобби", "играть", "вернут"))
        {
            return true;
        }

        if (texts == null)
        {
            return false;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            string normalizedText = StripRichText(text.text).ToLowerInvariant();
            if (ContainsAny(normalizedText, "exit", "quit", "menu", "back", "leave", "вых", "выйт", "назад"))
            {
                return false;
            }

            if (ContainsAny(normalizedText, "continue", "next game", "play again", "return to lobby", "lobby", "again", "продолж", "занов", "снов", "лобби", "играть", "вернут"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string input, params string[] tokens)
    {
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrWhiteSpace(token) && input.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static string StripRichText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        char[] buffer = new char[input.Length];
        int length = 0;
        bool insideTag = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '<')
            {
                insideTag = true;
            }
            else if (c == '>')
            {
                insideTag = false;
            }
            else if (!insideTag)
            {
                buffer[length++] = c;
            }
        }

        return new string(buffer, 0, length);
    }
}
