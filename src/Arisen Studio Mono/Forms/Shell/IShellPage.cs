namespace ArisenStudio.Shell
{
    /// <summary>
    /// Optional hook for a page shown in <see cref="PageHost" />. Implementing it is optional: PageHost accepts any Control.
    /// OnPageShown runs on the UI thread each time the page becomes the visible one. This is where a page reloads or lazy-loads its data (MW1-082 to MW1-094).
    /// </summary>
    internal interface IShellPage
    {
        void OnPageShown();
    }
}
