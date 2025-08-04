using System;
using System.Collections.Generic;
using UnityEngine;

public static class EventHub
{
    private static readonly Dictionary<Type, Delegate> eventTable = new Dictionary<Type, Delegate>();

    // Subscribe to an event
    public static void Subscribe<T>(Action<T> listener)
    {
        Delegate existing;
        if (eventTable.TryGetValue(typeof(T), out existing))
        {
            eventTable[typeof(T)] = Delegate.Combine(existing, listener);
        }
        else
        {
            eventTable[typeof(T)] = listener;
        }
    }

    // Unsubscribe from an event
    public static void Unsubscribe<T>(Action<T> listener)
    {
        if (eventTable.TryGetValue(typeof(T), out var existing))
        {
            var currentDel = Delegate.Remove(existing, listener);
            if (currentDel == null)
            {
                eventTable.Remove(typeof(T));
            }
            else
            {
                eventTable[typeof(T)] = currentDel;
            }
        }
    }

    // Publish (broadcast) an event
    public static void Publish<T>(T eventData)
    {
        if (eventTable.TryGetValue(typeof(T), out var del))
        {
            if (del is Action<T> callback)
            {
                callback.Invoke(eventData);
            }
            else
            {
                Debug.LogWarning($"[EventHub] Event type mismatch for {typeof(T)}");
            }
        }
        else
        {
            Debug.LogWarning($"[EventHub] No listeners for event type {typeof(T)}");
        }
    }
}
