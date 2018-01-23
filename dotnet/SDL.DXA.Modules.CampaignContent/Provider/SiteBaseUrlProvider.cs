namespace SDL.DXA.Modules.CampaignContent.Provider
{
    using Sdl.Web.Mvc.Configuration;

    /// <summary>
    /// Provider for site base URL. By default it returns the current localization path.
    /// It can be overriden for scenarios when need to have CDN based URLs for images etc.
    /// </summary>
    public class SiteBaseUrlProvider
    {
        public virtual string GetSiteBaseUrl()
        {
            return WebRequestContext.Localization.Path;
        }
    }
}