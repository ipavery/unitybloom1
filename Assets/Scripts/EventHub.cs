using System;
using System.Collections.Generic;

public static class EventHub
{
    private static readonly Dictionary<Type, Delegate> eventTable = new Dictionary<Type, Delegate>();

    // Subscribe to an event
    public static void Subscribe<T>(Action<T> listener)
    {
        if (!eventTable.ContainsKey(typeof(T)))
            eventTable[typeof(T)] = null;

        eventTable[typeof(T)] = (Action<T>)eventTable[typeof(T)] + listener;
    }

    // Unsubscribe from an event
    public static void Unsubscribe<T>(Action<T> listener)
    {
        if (eventTable.ContainsKey(typeof(T)))
            eventTable[typeof(T)] = (Action<T>)eventTable[typeof(T)] - listener;
    }

    // Publish (broadcast) an event
    public static void Publish<T>(T eventData)
    {
        if (eventTable.ContainsKey(typeof(T)))
        {
            var action = eventTable[typeof(T)] as Action<T>;
            action?.Invoke(eventData);
        }
    }
}
