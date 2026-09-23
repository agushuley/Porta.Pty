// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Porta.Pty.Tests;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Porta.Pty.Windows;

[TestClass]
public sealed class WindowsArgumentsTests
{
    [TestMethod]
    public void Format_RoundTripsArgumentsThroughCommandLineToArgvW()
    {
        if (!OperatingSystem.IsWindows()) return;

        string[][] cases =
        [
            ["-d", "Ubuntu"],
            [],
            ["", ""],
            ["two words", "tab\tvalue", "line\nvalue"],
            ["with\"quote", "C:\\path with space\\", "slashes\\\\\"quote"],
            ["\u041f\u0440\u0438\u0432\u0456\u0442", "emoji \U0001F600", "plain"],
        ];

        foreach (string[] args in cases)
        {
            CollectionAssert.AreEqual(args, ParseArgvTail(WindowsArguments.Format(args)));
        }
    }

    [TestMethod]
    public void CreateLaunch_UsesResolvedApplicationNameAndCanonicalArgvZero()
    {
        WindowsProcessLaunch launch = WindowsArguments.CreateLaunch(
            "C:\\Program Files\\Porta\\porta.exe",
            ["-d", "Ubuntu"],
            verbatim: false);

        launch.ApplicationName.Should().Be("C:\\Program Files\\Porta\\porta.exe");
        launch.CommandLine.Should().Be("\"C:\\Program Files\\Porta\\porta.exe\" -d Ubuntu");
    }

    [TestMethod]
    public void CreateLaunch_PreservesLegacyVerbatimArguments()
    {
        WindowsProcessLaunch launch = WindowsArguments.CreateLaunch(
            "app.exe",
            ["\"-d\"", "Ubuntu"],
            verbatim: true);

        launch.CommandLine.Should().Be("app.exe \"-d\" Ubuntu");
    }

    [TestMethod]
    public void SpawnAsync_RejectsNullAndNulArgumentsBeforeLaunching()
    {
        var nullArgument = ValidOptions();
        nullArgument.CommandLine = [null!];
        FluentActions.Invoking(() => { _ = global::Porta.Pty.PtyProvider.SpawnAsync(nullArgument, CancellationToken.None); })
            .Should().Throw<ArgumentException>();

        var nulArgument = ValidOptions();
        nulArgument.CommandLine = ["before\0after"];
        FluentActions.Invoking(() => { _ = global::Porta.Pty.PtyProvider.SpawnAsync(nulArgument, CancellationToken.None); })
            .Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void SpawnAsync_RejectsNulAppAndCwdBeforeLaunching()
    {
        var nulApp = ValidOptions();
        nulApp.App = "cmd\0.exe";
        FluentActions.Invoking(() => { _ = global::Porta.Pty.PtyProvider.SpawnAsync(nulApp, CancellationToken.None); })
            .Should().Throw<ArgumentException>();

        var nulCwd = ValidOptions();
        nulCwd.Cwd = "C:\\work\0dir";
        FluentActions.Invoking(() => { _ = global::Porta.Pty.PtyProvider.SpawnAsync(nulCwd, CancellationToken.None); })
            .Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public async Task SpawnAsync_WslReceivesSeparateDistributionAndEchoArguments()
    {
        if (!OperatingSystem.IsWindows() || !HasWslDistribution("Ubuntu")) return;

        var options = ValidOptions();
        options.App = Path.Combine(Environment.SystemDirectory, "wsl.exe");
        options.CommandLine = ["-d", "Ubuntu", "--", "/bin/echo", "-d", "Ubuntu"];

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using IPtyConnection terminal = await global::Porta.Pty.PtyProvider.SpawnAsync(options, cts.Token);
        string output = await ReadUntilAsync(terminal, "-d Ubuntu", cts.Token);

        output.Should().Contain("-d Ubuntu");
    }

    private static PtyOptions ValidOptions() => new()
    {
        App = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
        Cwd = Environment.CurrentDirectory,
        CommandLine = [],
        Environment = new System.Collections.Generic.Dictionary<string, string>(),
    };

    private static bool HasWslDistribution(string distribution)
    {
        string wsl = Path.Combine(Environment.SystemDirectory, "wsl.exe");
        if (!File.Exists(wsl)) return false;

        using var process = Process.Start(new ProcessStartInfo(wsl)
        {
            Arguments = "--list --quiet",
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });
        if (process == null || !process.WaitForExit(5_000) || process.ExitCode != 0) return false;

        string output = process.StandardOutput.ReadToEnd();
        return output.Split(['\0', '\r', '\n'])
            .Any(name => string.Equals(name, distribution, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> ReadUntilAsync(IPtyConnection terminal, string marker, CancellationToken ct)
    {
        var buffer = new byte[4_096];
        var output = new StringBuilder();
        while (!ct.IsCancellationRequested)
        {
            int count = await Task.Run(() => terminal.ReaderStream.Read(buffer, 0, buffer.Length), ct);
            if (count <= 0) break;

            output.Append(Encoding.UTF8.GetString(buffer, 0, count));
            if (output.ToString().Contains(marker, StringComparison.Ordinal)) break;
        }

        return output.ToString();
    }

    private static string[] ParseArgvTail(string commandLine)
    {
        string fullCommandLine = "porta-test.exe" + (commandLine.Length == 0 ? string.Empty : " " + commandLine);
        nint argv = CommandLineToArgvW(fullCommandLine, out int count);
        try
        {
            var result = new string[count - 1];
            for (int i = 1; i < count; i++)
            {
                result[i - 1] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size))!;
            }

            return result;
        }
        finally
        {
            if (argv != 0) LocalFree(argv);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CommandLineToArgvW(string commandLine, out int count);

    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint memory);
}
