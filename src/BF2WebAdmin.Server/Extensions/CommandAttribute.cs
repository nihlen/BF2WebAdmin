using BF2WebAdmin.Common;

namespace BF2WebAdmin.Server.Extensions;

[AttributeUsage(AttributeTargets.Class)]
public class CommandAttribute : Attribute
{
    public string[] Aliases { get; }
    public string[] Parameters { get; }
    public Auth AuthLevel { get; }
    public bool CombineLast { get;  }

    public CommandAttribute(string format, Auth authLevel, bool combineLast = true)
    {
        AuthLevel = authLevel;
        CombineLast = combineLast;

        if (!format.Contains(" "))
        {
            Aliases = SplitAliases(format);
        }
        else
        {
            var parts = format.Split(' ');
            Aliases = SplitAliases(parts[0]);
            Parameters = SplitParameters(parts);
        }
    }

    private static string[] SplitAliases(string firstPart)
    {
        var part = firstPart.Trim().ToLower();
        return part.Contains("|") ? part.Split('|') : new[] { part };
    }

    private static string[] SplitParameters(string[] parts)
    {
        return parts
            .Skip(1)
            .Select(p => p.Trim().Replace("<", "").Replace(">", ""))
            .ToArray();
    }
}