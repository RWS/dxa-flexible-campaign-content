﻿using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using SDL.DXA.Modules.CampaignContent.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Web;
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

        private static Dictionary<string, CampaignContentMarkup> cachedMarkup = new Dictionary<string, CampaignContentMarkup>();
        private static readonly ConcurrentDictionary<string, object> FileLocks = new ConcurrentDictionary<string, object>();

        private CampaignAssetProvider() {}

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
            cachedMarkup.TryGetValue(cacheKey, out campaignContentMarkup);
            string campaignBaseDir = GetBaseDir(localization, campaignId);
            if (campaignContentMarkup == null || !Directory.Exists(campaignBaseDir) || Directory.GetFiles(campaignBaseDir).Length == 0)
            {
                Log.Info("Extracting campaign " + campaignId + ", last modified = " + zipItem.LastModified);
                ExtractZip(zipItem, campaignBaseDir, zipItem.LastModified);
                campaignContentMarkup = GetMarkup(campaignBaseDir);
                campaignContentMarkup.LastModified = zipItem.LastModified;
                cachedMarkup[cacheKey] = campaignContentMarkup;
            }
            else if (campaignContentMarkup == null)
            {
                campaignContentMarkup = GetMarkup(campaignBaseDir);
                campaignContentMarkup.LastModified = zipItem.LastModified;
                cachedMarkup[cacheKey] = campaignContentMarkup;
            }
            else if (zipItem.LastModified > campaignContentMarkup.LastModified)
            {
                Log.Info("Zip has changed. Extracting campaign " + campaignId + ", last modified = " + zipItem.LastModified);
                ExtractZip(zipItem, campaignBaseDir, zipItem.LastModified);
                campaignContentMarkup = GetMarkup(campaignBaseDir);
                campaignContentMarkup.LastModified = zipItem.LastModified;
                cachedMarkup[cacheKey] = campaignContentMarkup;
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
            cachedMarkup.TryGetValue(cacheKey, out campaignContentMarkup);
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
        protected void ExtractZip(StaticContentItem zipItem, String directory, DateTime zipLastModified)
        {
            lock (GetLock(directory))
            {
                if (Directory.Exists(directory))
                {
                    if (Directory.GetCreationTime(directory) > zipLastModified && Directory.GetFiles(directory).Length > 0)
                    {
                        Log.Info("Campaign assets in directory '" + directory + "' is already up to date. Skipping recreation of campaign assets.");
                        return;
                    }

                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch (Exception e)
                    {
                        Log.Warn("Could not delete cached campaign resources in: " + directory, e);
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
                    Log.Warn("Could not unzip campaign resources in: " + directory + ". Will rely on the current content there.", e);
                }
            }

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

        /// <summary>
        /// Get a lock for use with a lock(){} block.
        /// </summary>
        /// <param name="name">Name of the lock</param>
        /// <returns>The lock object</returns>
        protected static Object GetLock(string name)
        {
            return FileLocks.GetOrAdd(name, s => new object());
        }

        private static string GetLocalStaticsFolder(string localizationId)
        {
            return string.Format("{0}\\{1}", StaticsFolder, localizationId);
        }
    }
}