using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using SDL.DXA.Modules.CampaignContent.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Web;
using System.Web.Configuration;
using Tridion.ContentDelivery.Meta;

namespace SDL.DXA.Modules.CampaignContent.Provider
{
    /// <summary>
    /// Campaign Asset Provider
    /// </summary>
    public class CampaignAssetProvider
    {
        private const string StaticsFolder = "BinaryData";

        private static CampaignAssetProvider _instance = null;

        private static readonly ConcurrentDictionary<string, CampaignContentMarkup> CachedMarkup = new ConcurrentDictionary<string, CampaignContentMarkup>();

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

        // Cache time to keep the ZIP file in staging sites.
        // This to avoid to have the ZIP file unzipped for each request on a XPM enabled staging site.
        // 
        private readonly int stagingCacheTime = 0;

        private CampaignAssetProvider()
        {
            var cacheTime = WebConfigurationManager.AppSettings["instant-campaign-staging-cache-time"];
            if (cacheTime != null)
            {
                stagingCacheTime = Int32.Parse(cacheTime);
            }
        }

        public static CampaignAssetProvider Instance
        {
            get
            {
                if ( _instance == null)
                {
                    _instance = new CampaignAssetProvider();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Get physical file name for a specific campaign asset
        /// </summary>
        /// <param name="localization"></param>
        /// <param name="campaignId"></param>
        /// <param name="assetUrl"></param>
        /// <returns></returns>
        public string GetAssetFileName(Localization localization, string campaignId, string assetUrl)
        {
            var assetFileName = GetBaseDir(localization, campaignId) + "/" + assetUrl;
            return assetFileName;
        }

        /// <summary>
        /// Get markup for a specific campaign
        /// </summary>
        /// <param name="campaignContentZip"></param>
        /// <param name="localization"></param>
        /// <returns></returns>
        public CampaignContentMarkup GetCampaignContentMarkup(CampaignContentZIP campaignContentZip, Localization localization)
        {
            StaticContentItem zipItem = GetZipItem(campaignContentZip, localization);
            return LoadCampaignContentMarkup(campaignContentZip.Id, zipItem, localization);
        }

        public CampaignContentMarkup GetCampaignContentMarkup(string campaignId, Localization localization)
        {
            StaticContentItem zipItem = GetZipItem(campaignId, localization);
            if (zipItem == null)
            {
                return null;
            }
            return LoadCampaignContentMarkup(campaignId, zipItem, localization);
        }

        /// <summary>
        /// Load markup for a specific campaign
        /// </summary>
        /// <param name="campaignId"></param>
        /// <param name="localization"></param>
        /// <returns></returns>
        protected CampaignContentMarkup LoadCampaignContentMarkup(string campaignId, StaticContentItem zipItem, Localization localization)
        {
            CampaignContentMarkup campaignContentMarkup;
            string cacheKey = GetMarkupCacheKey(campaignId, localization);
            CachedMarkup.TryGetValue(cacheKey, out campaignContentMarkup);
            string campaignBaseDir = GetBaseDir(localization, campaignId);

            if (campaignContentMarkup != null)
            {
                if (!localization.IsXpmEnabled && zipItem.LastModified > campaignContentMarkup.LastModified ||
                    localization.IsXpmEnabled && campaignContentMarkup.LastModified.AddSeconds(stagingCacheTime) < DateTime.Now)
                {
                    Log.Info("Zip has changed. Extracting campaign " + campaignId + ", last modified = " + zipItem.LastModified);
                    if (ExtractZip(zipItem, campaignBaseDir, zipItem.LastModified, firstTime: false))
                    {
                        campaignContentMarkup = GetMarkup(campaignBaseDir);
                        campaignContentMarkup.LastModified = zipItem.LastModified;
                        CachedMarkup[cacheKey] = campaignContentMarkup;
                    }
                }
            }
            else
            {
                if (!Directory.Exists(campaignBaseDir) || Directory.GetFiles(campaignBaseDir).Length == 0)
                {
                    Log.Info("Extracting campaign " + campaignId + ", last modified = " + zipItem.LastModified);
                    if (!ExtractZip(zipItem, campaignBaseDir, zipItem.LastModified, firstTime: true))
                    {
                        throw new DxaException($"Could not extract campaign with ID: {campaignId}");
                    }
                }

                campaignContentMarkup = GetMarkup(campaignBaseDir);
                campaignContentMarkup.LastModified = zipItem.LastModified;
                CachedMarkup[cacheKey] = campaignContentMarkup;
            }

            return campaignContentMarkup;
        }

        /// <summary>
        /// Get last modified date for a specific campaign and localization.
        /// </summary>
        /// <param name="campaignId"></param>
        /// <param name="localization"></param>
        /// <returns></returns>
        public DateTime GetLastModified(string campaignId, Localization localization)
        {
            string cacheKey = GetMarkupCacheKey(campaignId, localization);
            CampaignContentMarkup campaignContentMarkup;
            CachedMarkup.TryGetValue(cacheKey, out campaignContentMarkup);
            if ( campaignContentMarkup != null)
            {
                return campaignContentMarkup.LastModified;
            }
            else
            {
                return DateTime.Now;
            }         
        }

        /// <summary>
        /// Get markup cache key
        /// </summary>
        /// <param name="campaignId"></param>
        /// <param name="localization"></param>
        /// <returns></returns>
        private string GetMarkupCacheKey(string campaignId, Localization localization)
        {
            return campaignId + "-" + localization.Id;
        }

        /// <summary>
        /// Get ZIP item.
        /// </summary>
        /// <param name="campaignContentZip"></param>
        /// <returns></returns>
        protected StaticContentItem GetZipItem(CampaignContentZIP campaignContentZip, Localization localization)
        {
            return SiteConfiguration.ContentProvider.GetStaticContentItem(campaignContentZip.Url, localization);
        }

        protected StaticContentItem GetZipItem(string itemId, Localization localization)
        {
            BinaryMetaFactory binaryMetaFactory = new BinaryMetaFactory();
            BinaryMeta binaryMeta = binaryMetaFactory.GetMeta("tcm:" + localization.Id + "-" + itemId);
            if (binaryMeta != null)
            {
                return SiteConfiguration.ContentProvider.GetStaticContentItem(binaryMeta.UrlPath, localization);
            }
            return null;
        }

        /// <summary>
        /// Get base directory for the specified campaign ID
        /// </summary>
        /// <param name="localization"></param>
        /// <param name="campaignId"></param>
        /// <returns></returns>
        protected string GetBaseDir(Localization localization, string campaignId)
        {
            var webAppBaseDir = HttpContext.Current.Server.MapPath("~/");
            var baseDir = webAppBaseDir + GetLocalStaticsFolder(localization.Id) + "/campaign-content/" + campaignId;
            return baseDir;           
        }

        /// <summary>
        /// Extract campaign ZIP to specified directory.
        /// </summary>
        /// <param name="zipItem"></param>
        /// <param name="directory"></param>
        /// <param name="zipLastModified"></param>
        protected bool ExtractZip(StaticContentItem zipItem, String directory, DateTime zipLastModified, bool firstTime)
        {
            var fileLock = GetFileSemaphore(directory);

            if (fileLock.CurrentCount == 0 && !firstTime)
            {
                // Other thread is already working on it. Keep using the existing version until the new one is available
                //
                return false;
            }

            using (fileLock.UseWait())
            {
                if (Directory.Exists(directory))
                {
                    if (Directory.GetCreationTime(directory) > zipLastModified && Directory.GetFiles(directory).Length > 0)
                    {
                        Log.Info("Campaign assets in directory '" + directory + "' is already up to date. Skipping recreation of campaign assets.");
                        return false;
                    }

                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"Could not delete cached campaign resources in: {directory}. \nException: {e}");
                    }
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    using (ZipArchive archive = new ZipArchive(zipItem.GetContentStream()))
                    {
                        archive.ExtractToDirectory(directory);
                    }
                }
                catch (Exception e)
                {
                    Log.Warn($"Could not unzip campaign resources in: {directory}. Will rely on the current content there. \nException: {e}");
                    return false;
                }
            }
            return true;

        }

        /// <summary>
        /// Get campaign markup
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        protected CampaignContentMarkup GetMarkup(string directory)
        {
            var markup = new CampaignContentMarkup();

            // Main HTML
            //
            string mainHtmlFile = directory + "/index.html";
            if (System.IO.File.Exists(mainHtmlFile))
            {
                markup.MainHtml = System.IO.File.ReadAllText(mainHtmlFile);
            }

            // Header HTML
            //
            string headerHtmlFile = directory + "/header.html";
            if (System.IO.File.Exists(headerHtmlFile))
            {
                markup.HeaderHtml = System.IO.File.ReadAllText(headerHtmlFile);
            }

            // Footer HTML
            //
            string footerHtmlFile = directory + "/footer.html";
            if (System.IO.File.Exists(footerHtmlFile))
            {
                markup.FooterHtml = System.IO.File.ReadAllText(footerHtmlFile);
            }
            return markup;
        }

        protected static SemaphoreSlim GetFileSemaphore(string name)
        {
            return Semaphores.GetOrAdd(name, s => new SemaphoreSlim(1));
        }

        private static string GetLocalStaticsFolder(string localizationId)
        {
            return string.Format("{0}\\{1}", StaticsFolder, localizationId);
        }
    }


    // Extensions to SemaphoreSlim to make it possible to use it using blocks
    //
    static class SemaphoreSlimExtensions
    {
       public static IDisposable UseWait(
       this SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new ReleaseWrapper(semaphore);
        }

        private class ReleaseWrapper : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            private bool _isDisposed;

            public ReleaseWrapper(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (_isDisposed)
                    return;

                _semaphore.Release();
                _isDisposed = true;
            }
        }
    }
}