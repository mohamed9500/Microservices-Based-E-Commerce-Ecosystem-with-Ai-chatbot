namespace Nexus.Contracts;

/// <summary>
/// Marks an integration event and defines its versioned routing key, e.g. "order.created.v1".
/// Breaking changes must introduce a new version (new type + new attribute version), never mutate an old one.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class EventContractAttribute(string name, int version) : Attribute
{
    public string Name { get; } = name;
    public int Version { get; } = version;
    public string RoutingKey => $"{Name}.v{Version}";

    public static EventContractAttribute For<T>() => For(typeof(T));

    public static EventContractAttribute For(Type type) =>
        (EventContractAttribute?)GetCustomAttribute(type, typeof(EventContractAttribute))
        ?? throw new InvalidOperationException($"{type.Name} is missing [EventContract].");

    public static string RoutingKeyFor<T>() => For<T>().RoutingKey;
}
