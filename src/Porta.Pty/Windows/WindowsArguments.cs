// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Porta.Pty.Windows
{
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Helper class for serializing an argv tail into the command-line string CreateProcessW accepts.
    /// </summary>
    internal static class WindowsArguments
    {
        /// <summary>
        /// Serializes arguments according to the CommandLineToArgvW-compatible quoting rules used by
        /// the Windows C runtime. The result round-trips empty arguments, embedded quotes, and runs of
        /// backslashes immediately before a quote or the closing quote.
        /// </summary>
        /// <param name="args">The command line arguments to format.</param>
        /// <returns>A space-delimited command-line representation of <paramref name="args"/>.</returns>
        public static string Format(params string[]? args) =>
            string.Join(" ", (args ?? Enumerable.Empty<string>()).Select(Format));

        /// <summary>
        /// Joins preformatted command-line fragments without modification.
        /// </summary>
        /// <param name="args">The command line arguments to format.</param>
        /// <returns>A space-delimited list of command line arguments.</returns>
        public static string FormatVerbatim(params string[]? args) =>
            string.Join(" ", args ?? Enumerable.Empty<string>());

        internal static WindowsProcessLaunch CreateLaunch(string applicationName, string[] commandLine, bool verbatim)
        {
            string arguments = verbatim ? FormatVerbatim(commandLine) : Format(commandLine);
            string fullCommandLine = Format(applicationName);
            if (arguments.Length > 0)
            {
                fullCommandLine += " " + arguments;
            }

            return new WindowsProcessLaunch(applicationName, fullCommandLine);
        }

        private static string Format(string arg)
        {
            if (arg.Length > 0 && !arg.Any(char.IsWhiteSpace) && !arg.Contains('"'))
            {
                return arg;
            }

            var result = new StringBuilder(arg.Length + 2);
            result.Append('"');
            int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }

                if (c == '"')
                {
                    result.Append('\\', (slashes * 2) + 1);
                    result.Append(c);
                }
                else
                {
                    result.Append('\\', slashes);
                    result.Append(c);
                }

                slashes = 0;
            }

            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }
    }

    /// <summary>The resolved target and command line passed to CreateProcessW.</summary>
    internal sealed record WindowsProcessLaunch(string ApplicationName, string CommandLine);
}
