namespace ScatoloneDownloader
{
    /// <summary>
    /// Builds the "done/total" counter that goes inside a Spectre progress task
    /// description.
    /// <para>
    /// Spectre parses a task description as MARKUP, so a literal <c>[26/30151]</c>
    /// is read as a style tag and the render thread throws
    /// <c>Could not find color or style '26/30151'</c> — an unhandled exception on
    /// a background thread, which kills the process partway through a long run.
    /// The brackets have to be escaped as <c>[[</c>/<c>]]</c>, and doing that by
    /// hand at each call site is exactly the step that gets forgotten, so every
    /// progress counter goes through here instead.
    /// </para>
    /// </summary>
    internal static class ProgressLabel
    {
        /// <summary>A markup-safe <c>[done/total]</c> counter.</summary>
        internal static string Counter(double done, int total)
        {
            return $"[[{(long)done}/{total}]]";
        }
    }
}
