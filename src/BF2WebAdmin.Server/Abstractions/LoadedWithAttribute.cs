namespace BF2WebAdmin.Server.Abstractions;

/// <summary>
/// A module that is part of another: a server group that enables <see cref="Module"/> gets this one too, and the list of
/// modules to enable (the web UI's) does not show it on its own. Heli2v2's practice commands are such modules.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class LoadedWithAttribute(Type module) : Attribute
{
    public Type Module { get; } = module;
}
