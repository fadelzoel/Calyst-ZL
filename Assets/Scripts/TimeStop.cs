using System;
using UnityEngine;

/// <summary>Global time-stop flag. Enemies (and later projectiles) subscribe to Started/Ended.</summary>
public static class TimeStop
{
    public static bool Active { get; private set; }
    public static event Action Started;
    public static event Action Ended;

    // Keeps the flag clean when Domain Reload is disabled in the editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        Started = null;
        Ended = null;
    }

    public static void Begin()
    {
        if (Active) return;
        Active = true;
        Started?.Invoke();
    }

    public static void End()
    {
        if (!Active) return;
        Active = false;
        Ended?.Invoke();
    }
}