using System.Reflection;

namespace MemoryOfMemorieCodexBridge.Probing;

internal sealed class RuntimeProbe
{
    private const BindingFlags MethodFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal RuntimeSnapshot Capture()
    {
        var types = DiscoverTypes();
        var capabilities = ActionCatalog.Candidates.Select(action => CreateCapability(action, types)).ToArray();
        return new RuntimeSnapshot(DateTimeOffset.UtcNow, capabilities, types.Values.OrderBy(type => type.TypeName).ToArray());
    }

    private static Dictionary<string, TypeProbe> DiscoverTypes()
    {
        var targetNames = ActionCatalog.Candidates.Select(candidate => candidate.TargetType).Distinct();
        var types = new Dictionary<string, TypeProbe>(StringComparer.Ordinal);
        foreach (var targetName in targetNames)
        {
            types[targetName] = CreateTypeProbe(targetName);
        }

        return types;
    }

    private static TypeProbe CreateTypeProbe(string targetName)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(targetName, false)).FirstOrDefault(found => found is not null);
        if (type is null)
        {
            return new TypeProbe(targetName, false, []);
        }

        var methods = type.GetMethods(MethodFlags)
            .Select(method => new MethodProbe(method.Name, method.GetParameters().Select(parameter => parameter.ParameterType.Name).ToArray(), method.ReturnType.Name))
            .OrderBy(method => method.Name)
            .ToArray();
        return new TypeProbe(targetName, true, methods);
    }

    private static CapabilityProbe CreateCapability(ActionDefinition action, IReadOnlyDictionary<string, TypeProbe> types)
    {
        var type = types[action.TargetType];
        var methodFound = type.Methods.Any(method => method.Name == action.TargetMethod);
        var state = methodFound ? "candidate_unverified" : "unavailable";
        return new CapabilityProbe(action.Id, action.Category, action.Description, type.Found, methodFound, state);
    }
}
