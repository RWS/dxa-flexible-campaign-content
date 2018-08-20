using Sdl.Web.Common.Logging;
using Sdl.Web.Mvc.Configuration;
using SDL.DXA.Modules.CampaignContent.Provider;
using System;
using System.Net;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace SDL.DXA.Modules.CampaignContent.Controllers
{
    /// <summary>
    /// Campaign Asset Controller
    /// </summary>
    public class CampaignAssetController : Controller
    {
        private TimeSpan _assetMaxAge;

        const int DEFAULT_ASSET_MAX_AGE_HOURS = 1;

        public CampaignAssetController()
        {
            var assetMaxAgeHours = WebConfigurationManager.AppSettings["instant-campaign-asset-max-age-hours"];
            _assetMaxAge = new TimeSpan(0, assetMaxAgeHours != null? Int32.Parse(assetMaxAgeHours) : DEFAULT_ASSET_MAX_AGE_HOURS , 0, 0);
        }

        /// <summary>
        /// Get campaign asset
        /// </summary>
        /// <param name="campaignId"></param>
        /// <param name="assetUrl"></param>
        /// <returns></returns>
        public FileResult GetAsset(string campaignId, string assetUrl)
        {
            // Get file asset based on localization
            //
            var assetFileName = CampaignAssetProvider.Instance.GetAssetFileName(WebRequestContext.Localization, campaignId, assetUrl);

            // Get last modified timestamp on the campaign (the campaign ZIP multi media item)
            //
            var lastModified = CampaignAssetProvider.Instance.GetLastModified(campaignId, WebRequestContext.Localization);

            if ( IsToBeRefreshed(lastModified) )
            {
                // Return asset
                //
                return File(assetFileName, MimeMapping.GetMimeMapping(assetFileName));
            }
            return null;
        }

        /// <summary>
        /// Check if assets needs to be refreshed
        /// </summary>
        /// <param name="lastModified"></param>
        /// <returns></returns>
        private bool IsToBeRefreshed(DateTime lastModified)
        {
            var request = HttpContext.Request;
            var response = HttpContext.Response;

            DateTime ifModifiedSince = Convert.ToDateTime(request.Headers["If-Modified-Since"]);
            if (lastModified <= ifModifiedSince.AddSeconds(1))
            {
                Log.Debug("Campaign asset last modified at {0} => Sending HTTP 304 (Not Modified).", lastModified);
                response.StatusCode = (int)HttpStatusCode.NotModified;
                response.SuppressContent = true;
                return false;
            }
            else
            {
                TimeSpan maxAge = _assetMaxAge;
                response.Cache.SetLastModified(lastModified); // Allows the browser to do an If-Modified-Since request next time
                response.Cache.SetCacheability(HttpCacheability.Public); // Allow caching
                response.Cache.SetMaxAge(maxAge);
                response.Cache.SetExpires(DateTime.UtcNow.Add(maxAge));
                return true;
            }
        }
    }
}