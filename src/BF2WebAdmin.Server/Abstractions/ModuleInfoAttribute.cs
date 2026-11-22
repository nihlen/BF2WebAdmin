namespace BF2WebAdmin.Server.Abstractions;

/// <summary>
/// A module's name and description in words, for the web UI's module list (<see cref="ModuleResolver.GetModuleInfos"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModuleInfoAttribute(string displayName, string description) : Attribute
{
    public string DisplayName { get; } = displayName;

    /// <summary>What the module does for a server owner, in one or two short sentences.</summary>
    public string Description { get; } = description;
}
