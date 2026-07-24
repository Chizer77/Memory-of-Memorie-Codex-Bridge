namespace MemoryOfMemorieCodexBridge.Probing;

internal sealed class MethodProbe
{
    internal MethodProbe(string name, IReadOnlyList<string> parameters, string returnType)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters { get; }

    public string ReturnType { get; }
}

internal sealed class TypeProbe
{
    internal TypeProbe(string typeName, bool found, IReadOnlyList<MethodProbe> methods)
    {
        TypeName = typeName;
        Found = found;
        Methods = methods;
    }

    public string TypeName { get; }

    public bool Found { get; }

    public IReadOnlyList<MethodProbe> Methods { get; }
}

internal sealed class CapabilityProbe
{
    internal CapabilityProbe(string id, string category, string description, bool typeFound, bool methodFound, string state)
    {
        Id = id;
        Category = category;
        Description = description;
        TypeFound = typeFound;
        MethodFound = methodFound;
        State = state;
    }

    public string Id { get; }

    public string Category { get; }

    public string Description { get; }

    public bool TypeFound { get; }

    public bool MethodFound { get; }

    public string State { get; }
}

internal sealed class RuntimeSnapshot
{
    internal RuntimeSnapshot(DateTimeOffset capturedAt, IReadOnlyList<CapabilityProbe> capabilities, IReadOnlyList<TypeProbe> types)
    {
        CapturedAt = capturedAt;
        Capabilities = capabilities;
        Types = types;
    }

    public DateTimeOffset CapturedAt { get; }

    public IReadOnlyList<CapabilityProbe> Capabilities { get; }

    public IReadOnlyList<TypeProbe> Types { get; }
}
