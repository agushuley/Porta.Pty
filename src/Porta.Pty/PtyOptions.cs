// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Porta.Pty
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Options for spawning a new pty process.
    /// </summary>
    public class PtyOptions
    {
        /// <summary>
        /// Gets or sets the terminal name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the number of initial rows.
        /// </summary>
        public int Rows { get; set; }

        /// <summary>
        /// Gets or sets the number of initial columns.
        /// </summary>
        public int Cols { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the spawned process.
        /// </summary>
        public string Cwd { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the process to be spawned.
        /// </summary>
        public string App { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unescaped argv tail for the process. Every entry must be non-null and
        /// must not contain NUL. Porta serializes these values for the target platform; callers must
        /// not add Windows command-line quoting themselves.
        /// </summary>
        public string[] CommandLine { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="CommandLine"/> contains preformatted
        /// command-line fragments that must be joined without serialization. This legacy escape hatch
        /// is unsafe for ordinary process launches; supply unescaped argv entries instead.
        /// </summary>
        [Obsolete("CommandLine is an unescaped argv tail. Do not preformat arguments; this compatibility escape hatch will be removed in a future major release.")]
        public bool VerbatimCommandLine { get; set; }

        /// <summary>
        /// Gets or sets the process' environment variables.
        /// </summary>
        public IDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets a value indicating whether reads and writes should complete without holding
        /// a thread. Off by default.
        /// </summary>
        /// <remarks>
        /// The same guarantee on every platform, which is the only thing that makes it worth being
        /// one option rather than two: with this set, awaiting ReadAsync on an idle session occupies
        /// no thread, so a process can hold many sessions open at a cost that does not scale with
        /// how many of them are quiet.
        ///
        /// How that is achieved differs. Unix puts the controller into non-blocking mode and shares
        /// one poll(2) loop, plus one reaper in place of a waitpid thread per child. Windows uses
        /// overlapped pipes and the I/O completion port. What a caller can rely on does not differ,
        /// and both are implemented -- an earlier revision of this documentation promised the
        /// guarantee on Windows before it was true.
        ///
        /// Opt-in because it changes the I/O path underneath every existing consumer. The default
        /// path is unchanged: a blocking descriptor, and ReadAsync serviced by the thread pool.
        ///
        /// Needs Linux 5.3 or newer, for the pidfd_open used to watch a child exit; spawning with
        /// this set throws PlatformNotSupportedException on anything older rather than quietly
        /// falling back to something slower. Of the distributions .NET 10 supports, only RHEL 8
        /// ships an older kernel. macOS and Windows have no such floor.
        ///
        /// Synchronous Read and Write keep working either way, and still block the calling thread.
        /// This is about what ASYNC costs, not about removing the sync API.
        /// </remarks>
        public bool UseAsyncIo { get; set; }
    }
}
