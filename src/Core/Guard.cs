using System;

namespace Nocturne;

internal sealed class Guard
{
    private readonly string _tag;
    private readonly Action[] _steps;
    private readonly int[] _errs;

    internal Guard(string tag, params Action[] steps)
    {
        _tag = tag;
        _steps = steps;
        _errs = new int[steps.Length];
    }

    internal void Run()
    {
        for (int i = 0; i < _steps.Length; i++)
        {
            try
            {
                _steps[i]();
            }
            catch (Exception ex)
            {
                int n = ++_errs[i];
                if (n == 1 || n % 1000 == 0)
                    NocturnePlugin.Logger?.LogWarning($"[{_tag}] {_steps[i].Method.DeclaringType?.Name}.{_steps[i].Method.Name} threw ({n}): {ex}");
            }
        }
    }
}
