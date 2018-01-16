namespace SDL.DXA.Modules.CampaignContent.Provider
{
    using Sdl.Web.Mvc.Configuration;

    public class SiteBaseUrlProvider
    {
        public virtual string GetSiteBaseUrl()
        {
            return WebRequestContext.Localization.Path;
        }
    }
}