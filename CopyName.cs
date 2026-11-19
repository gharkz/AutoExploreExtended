using System;
using System.Text.RegularExpressions;

namespace AutoExploreExtended
{
    // Name for a duplicate: name_2, name_3, ... (a name already ending in _N counts up from its base).
    internal static class CopyName
    {
        internal static string Next(string name, Func<string, bool> taken)
        {
            string stem = Regex.Replace(name, @"_\d+$", "");
            for (int i = 2; ; i++) { string n = stem + "_" + i; if (!taken(n)) return n; }
        }
    }
}
