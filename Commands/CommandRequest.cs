namespace MemoryOfMemorieCodexBridge.Commands;

internal sealed class CommandRequest
{
    public string Id { get; set; }

    public string Command { get; set; }

    public int Minutes { get; set; }

    internal string CommandId => string.IsNullOrWhiteSpace(Id) ? Command : Id;
}
