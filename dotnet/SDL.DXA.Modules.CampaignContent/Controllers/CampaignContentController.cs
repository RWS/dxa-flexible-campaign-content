using NSoup;
using NSoup.Nodes;
using Sdl.Web.Common;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Controllers;
using SDL.DXA.Modules.CampaignContent.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Web.Mvc;

namespace SDL.DXA.Modules.CampaignContent.Controllers
{
    /// <summary>
    /// Campaign Content Controller
    /// </summary>
    public class CampaignContentController : BaseController
    {

        private static Dictionary<string, CampaignContentMarkup> cachedMarkup = new Dictionary<string, CampaignContentMarkup>();
        private static readonly ConcurrentDictionary<string, object> FileLocks = new ConcurrentDictionary<string, object>();

        // TODO: Have some kind of cleanup thread that clean up not used campaigns
        // TODO: Return HTTP-304 for all assets when client already has them
	// TODO: Create a proxy instead to have cleaner URLs and the possibility to return HTTP-304
	
        /// <summary>
        /// Assembly Content
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="containerSize"></param>
        /// <returns></returns>
        public ActionResult AssemblyContent(EntityModel entity, int containerSize = 0)
        {
            SetupViewData(entity, containerSize);

            CampaignContentZIP contentZip = (CampaignContentZIP) entity;

            // Process markup
            //
            ProcessMarkup(contentZip);

            return View(entity.MvcData.ViewName, entity);
        }

        /// <summary>
        /// Process markup. Blend the markup in the ZIP file and CMS content.
        /// </summary>
        /// <param name="campaignContentZip"></param>
        protected void ProcessMarkup(CampaignContentZIP campaignContentZip)
        {
            StaticContentItem zipItem = GetZipItem(campaignContentZip);

            CampaignContentMarkup campaignContentMarkup;
            HtmlHolder htmlMarkup;

            //SiteConfiguration.CacheProvider.TryGet<CampaignContentMarkup>(campaignContentZip.Id, "CampaignContent", out campaignContentMarkup);

            string cacheKey = campaignContentZip.Id + "-" + WebRequestContext.Localization.LocalizationId;

            cachedMarkup.TryGetValue(cacheKey, out campaignContentMarkup);
            string campaignBaseDir = GetBaseDir(campaignContentZip);
            if ( campaignContentMarkup == null || !Directory.Exists(campaignBaseDir) || Directory.GetFiles(campaignBaseDir).Length == 0 )
            {
                Log.Info("Extracting campaign " + campaignContentZip.Id + ", last modified = " + zipItem.LastModified);
                ExtractZip(zipItem, campaignBaseDir, zipItem.LastModified);
                htmlMarkup = GetMarkup(campaignBaseDir);
                campaignContentMarkup = new CampaignContentMarkup { HtmlMarkup = htmlMarkup, LastModified = zipItem.LastModified };
                //SiteConfiguration.CacheProvider.Store<CampaignContentMarkup>(campaignContentZip.Id, "CampaignContent", campaignContentMarkup);
                cachedMarkup[cacheKey] = campaignContentMarkup;
            }
            else if ( campaignContentMarkup == null )
            {
                htmlMarkup = GetMarkup(campaignBaseDir);
                campaignContentMarkup = new CampaignContentMarkup { HtmlMarkup = htmlMarkup, LastModified = zipItem.LastModified };
                cachedMarkup[cacheKey] = campaignContentMarkup;
            }
            else
            {
                htmlMarkup = campaignContentMarkup.HtmlMarkup;
            }

            // Throw exception if main HTML is not found
            //
            if ( htmlMarkup.MainHtml == null )
            {
                throw new DxaException("No markup defined for campaign with ID: " + campaignContentZip.Id);
            }

            var htmlDoc = NSoupClient.Parse("<body>" + htmlMarkup.MainHtml + "</body>");

            // Inject content into placeholders in the markup
            //
            if (campaignContentZip.TaggedContent != null)
            {
                int index = 1;
                foreach (var taggedContent in campaignContentZip.TaggedContent)
                {
                    foreach (var element in htmlDoc.Body.Select("[data-content-name=" + taggedContent.Name + "]"))
                    {
                        String contentMarkup =
                                "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedContent[" +
                                index +
                                "]/custom:content[1]\"} -->" +
                                taggedContent.Content.ToString();
                        element.Html(contentMarkup);
                    }
                    index++;
                }
            }

            // Inject tagged properties
            //
            if ( campaignContentZip.TaggedProperties != null)
            {
                foreach (var taggedProperty in campaignContentZip.TaggedProperties)
                {
                    foreach (var element in htmlDoc.Body.Select("[data-property-name=" + taggedProperty.Name + "]"))
                    {
                        element.Attr(taggedProperty.Target, taggedProperty.Value);
                    }
                }
            }

            string assetBaseDir = this.GetAssetBaseDir(campaignContentZip);

            // Process assets
            //
            this.ProcessAssetLinks(htmlDoc, assetBaseDir, "href");
            this.ProcessAssetLinks(htmlDoc, assetBaseDir, "src");

            // Inject tagged images
            //
            if (campaignContentZip.TaggedImages != null)
            {
                int index = 1;
                foreach (var taggedImage in campaignContentZip.TaggedImages)
                {
                    foreach (var element in htmlDoc.Body.Select("[data-image-name=" + taggedImage.Name + "]"))
                    {
                        String xpmMarkup =
                                 "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedImages[" +
                                index +
                                "]/custom:image[1]\"} -->";
                        element.Attr("src", taggedImage.Image.Url);
                        element.Before(xpmMarkup);
                    }
                    index++;
                }
            }

            // Insert header markup (JS, CSS etc)
            //
            if ( htmlMarkup.HeaderHtml != null )
            {
                var headerDoc = NSoupClient.Parse("<body>" + htmlMarkup.HeaderHtml + "</body>");
                this.ProcessAssetLinks(headerDoc, assetBaseDir, "src");
                this.ProcessAssetLinks(headerDoc, assetBaseDir, "href");
                PluggableMarkup.RegisterMarkup("css", headerDoc.Body.Html());
                // TODO: Should it be called top-js?? Or have a top-css injection point as well
            }

            // Insert footer markup (JS etc)
            //
            if ( htmlMarkup.FooterHtml != null )
            {
                var footerDoc = NSoupClient.Parse("<body>" + htmlMarkup.FooterHtml + "</body>");
                this.ProcessAssetLinks(footerDoc, assetBaseDir, "src");
                PluggableMarkup.RegisterMarkup("bottom-js", footerDoc.Body.Html());
            }

            campaignContentZip.ProcessedContent = new RichTextFragment(htmlDoc.Body.Html()).ToHtml();

        }

        /// <summary>
        /// Process asset links so they refer to correct exposed campaign path
        /// </summary>
        /// <param name="htmlDoc"></param>
        /// <param name="assetBaseDir"></param>
        /// <param name="attributeName"></param>
        protected void ProcessAssetLinks(Document htmlDoc, String assetBaseDir, String attributeName)
        {
            foreach (var element in htmlDoc.Body.Select("[" + attributeName + "]"))
            {
                String assetUrl = element.Attr(attributeName);
                if (!assetUrl.StartsWith("/") && !assetUrl.StartsWith("http") && !assetUrl.StartsWith("#"))
                {
                    assetUrl = assetBaseDir + "/" + assetUrl;
                    element.Attr(attributeName, assetUrl);
                }
            }
        }

        /// <summary>
        /// Get ZIP item.
        /// </summary>
        /// <param name="campaignContentZip"></param>
        /// <returns></returns>
        protected StaticContentItem GetZipItem(CampaignContentZIP campaignContentZip)
        {
            return SiteConfiguration.ContentProvider.GetStaticContentItem(campaignContentZip.Url, WebRequestContext.Localization);
        }

        /// <summary>
        /// Get base directory for the specified campaign ZIP
        /// </summary>
        /// <param name="campaignContentZip"></param>
        /// <returns></returns>
        protected string GetBaseDir(CampaignContentZIP campaignContentZip)
        {
            // TODO: Have the asset base configurable

            return HttpContext.Server.MapPath("~/campaign-content/assets/" + WebRequestContext.Localization.LocalizationId + "/" + campaignContentZip.Id);
        }

        /// <summary>
        /// Get asset base dir (which are exposed on the web page) for the specified campaign ZIP
        /// </summary>
        /// <param name="campaignContentZip"></param>
        /// <returns></returns>
        protected String GetAssetBaseDir(CampaignContentZIP campaignContentZip)
        {
            return "/campaign-content/assets/" + WebRequestContext.Localization.LocalizationId + "/" + campaignContentZip.Id;
        }

        /// <summary>
        /// Extract campaign ZIP to specified directory.
        /// </summary>
        /// <param name="zipItem"></param>
        /// <param name="directory"></param>
        /// <param name="zipLastModified"></param>
        protected void ExtractZip(StaticContentItem zipItem, String directory, DateTime zipLastModified)
        { 
            lock ( GetLock(directory) )
            {
                if (Directory.Exists(directory))
                {
                    if ( Directory.GetCreationTime(directory) > zipLastModified && Directory.GetFiles(directory).Length > 0 )
                    {
                        Log.Info("Campaign assets in directory '" + directory + "' is already up to date. Skipping recreation of campaign assets.");
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
        protected HtmlHolder GetMarkup(string directory)
        {
            HtmlHolder htmlHolder = new HtmlHolder();

            // Main HTML
            //
            string mainHtmlFile = directory + "/index.html";
            if (System.IO.File.Exists(mainHtmlFile))
            {
                htmlHolder.MainHtml = System.IO.File.ReadAllText(mainHtmlFile);
            }

            // Header HTML
            //
            string headerHtmlFile = directory + "/header.html";
            if (System.IO.File.Exists(headerHtmlFile))
            {
                htmlHolder.HeaderHtml = System.IO.File.ReadAllText(headerHtmlFile);
            }

            // Footer HTML
            //
            string footerHtmlFile = directory + "/footer.html";
            if (System.IO.File.Exists(footerHtmlFile))
            {
                htmlHolder.FooterHtml = System.IO.File.ReadAllText(footerHtmlFile);
            }
            return htmlHolder;
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

    }

    /// <summary>
    /// Holder class for campaign content
    /// </summary>
    public class CampaignContentMarkup
    {
        internal HtmlHolder HtmlMarkup { get; set; }
        internal DateTime LastModified { get; set; }
    }

    /// <summary>
    /// Holder class for the different HTML fragments in a campaign
    /// </summary>
    public class HtmlHolder
    {
        internal string HeaderHtml { get; set; }
        internal string MainHtml { get; set; }
        internal string FooterHtml { get; set; }
    }
}
