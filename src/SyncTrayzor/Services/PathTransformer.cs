using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Pri.LongPath;

namespace SyncTrayzor.Services
{
    public interface IPathTransformer
    {
        string MakeAbsolute(string input);
    }

    public class PathTransformer : IPathTransformer
    {
        private static readonly Regex varRegex = new(@"%(\w+)%");
        private readonly Dictionary<string, string> specials;
        private readonly string basePath;

        public PathTransformer(IAssemblyProvider assemblyProvider)
        {
            basePath = Path.GetDirectoryName(assemblyProvider.Location);

            specials = new Dictionary<string, string>()
            {
                // This is legacy, in case it's managed to slip through the configuration
                ["EXEPATH"] = basePath,
            };
        }

        public string MakeAbsolute(string input)
        {
            if (String.IsNullOrWhiteSpace(input))
                return input;

            var transformed = varRegex.Replace(input, match =>
            {
                var name = match.Groups[1].Value;
                if (specials.TryGetValue(name, out string value))
                    return value;
                return Environment.GetEnvironmentVariable(name);
            });

            if (!Path.IsPathRooted(transformed))
                transformed = Path.Combine(basePath, transformed);

            return transformed;
        }
    }
}
