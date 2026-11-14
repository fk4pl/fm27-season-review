using System;

namespace SeasonReview
{
    /// <summary>
    /// Guard for code that must stay read-only on disk, such as the in-game panel: inside a
    /// <see cref="Forbid"/> scope every writer of this mod (report files, the list of reported
    /// seasons, the raw dump) refuses to run. Writers call <see cref="Demand"/> before they touch
    /// the disk or change what they remember.
    /// </summary>
    internal static class DiskWrites
    {
        [ThreadStatic] private static int _forbidden;
        [ThreadStatic] private static string _reason;

        public static bool Forbidden => _forbidden > 0;

        /// <summary>Forbids writing on this thread until the returned scope is disposed. Scopes can nest.</summary>
        public static IDisposable Forbid(string reason)
        {
            if (_forbidden++ == 0) _reason = reason;
            return new Scope();
        }

        /// <summary>Throws when called inside a <see cref="Forbid"/> scope.</summary>
        public static void Demand(string what)
        {
            if (_forbidden > 0)
                throw new InvalidOperationException($"Refusing to write {what}: disk writes are forbidden here ({_reason}).");
        }

        private sealed class Scope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (--_forbidden == 0) _reason = null;
            }
        }
    }
}
