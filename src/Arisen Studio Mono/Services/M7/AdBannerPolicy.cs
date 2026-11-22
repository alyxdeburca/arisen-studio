using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Services.M7
{
    // M7 boundary: ads (AdsJumbo.WinForm.WPF 1.5.0, AdsJumboWinForm.BannerAds control; WPF-hosted, Windows-only).
    // Old use, in MainWindow (the only place): a BannerAds control named BannerAdsDashboard sits in PanelAdPlacement on the
    // dashboard, with ApplicationId "33krwjd74qn6" set in the designer. After the dashboard loads, the code calls
    // ShowAd(728, 90, "33krwjd74qn6") and then ShowAd(control width, control height, "33krwjd74qn6").
    // There was no "ads off" setting in the old code that this port saw; whether the Mono build shows ads at all is an
    // owner decision (see the report).

    /// <summary>
    /// What the app does about ads: whether to show them, where the ad unit is and the size.
    /// </summary>
    public interface IAdBannerPolicy
    {
        /// <summary>False for the stub: the dashboard should leave the ad area out (collapse the panel).</summary>
        bool AdsEnabled { get; }

        /// <summary>The ad unit id the old code used.</summary>
        string ApplicationId { get; }

        /// <summary>The banner size of the first old ShowAd call.</summary>
        Size BannerSize { get; }

        /// <summary>
        /// Make a banner control for the dashboard, or null when ads are off. The caller docks it in the ad panel.
        /// </summary>
        Control CreateBanner();

        /// <summary>
        /// Load an ad into the banner made by CreateBanner (old: ShowAd(width, height, applicationId)). No-op when ads are
        /// off. Does not throw.
        /// </summary>
        void ShowAd(Control banner, int width, int height);
    }

    /// <summary>
    /// Stub policy: ads are off. CreateBanner returns null and ShowAd does nothing. Default of M7Services.Ads.
    /// The values are the old ones so a real implementation can reuse them.
    /// </summary>
    public sealed class AdsDisabledBannerPolicy : IAdBannerPolicy
    {
        /// <summary>The ApplicationId in the old MainWindow designer and ShowAd calls.</summary>
        public const string DefaultApplicationId = "33krwjd74qn6";

        public bool AdsEnabled
        {
            get { return false; }
        }

        public string ApplicationId
        {
            get { return DefaultApplicationId; }
        }

        public Size BannerSize
        {
            get { return new Size(728, 90); }
        }

        public Control CreateBanner()
        {
            return null;
        }

        public void ShowAd(Control banner, int width, int height)
        {
        }
    }
}
