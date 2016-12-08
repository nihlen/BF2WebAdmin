using System;
using System.Text.RegularExpressions;

namespace BF2WebAdmin.Server.Extensions
{
    public static class ProjectContext
    {
        public static string GetDirectory()
        {
            var dir = AppContext.BaseDirectory;

            dir = Regex.Replace(dir, @"(\\|\/)+bin(\\|\/)+.+$", string.Empty, 
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return dir;
        }

    }
}