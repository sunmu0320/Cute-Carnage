using System;
using UnityEngine;

/// <summary>Raised by fences, towers and the base core whenever they take damage (UI hit-direction indicator).</summary>
public static class StructureDamageEvents
{
    public static event Action<Component> Damaged;

    public static void Report(Component structure)
    {
        Damaged?.Invoke(structure);
    }
}
