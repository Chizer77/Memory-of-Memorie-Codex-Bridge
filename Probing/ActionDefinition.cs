namespace MemoryOfMemorieCodexBridge.Probing;

internal sealed class ActionDefinition
{
    internal ActionDefinition(string id, string category, string targetType, string targetMethod, string description)
    {
        Id = id;
        Category = category;
        TargetType = targetType;
        TargetMethod = targetMethod;
        Description = description;
    }

    public string Id { get; }

    public string Category { get; }

    public string TargetType { get; }

    public string TargetMethod { get; }

    public string Description { get; }
}
