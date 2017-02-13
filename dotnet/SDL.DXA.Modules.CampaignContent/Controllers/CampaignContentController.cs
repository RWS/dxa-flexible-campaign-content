using NSoup;
using NSoup.Nodes;
using Sdl.Web.Common.Configuration;
using Sdl.Web.Common.Logging;
using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using Sdl.Web.Mvc.Controllers;
using SDL.DXA.Modules.CampaignContent.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SDL.DXA.Modules.CampaignContent.Controllers
{
    /// <summary>
    /// Campaign Content Controller
    /// </summary>
    public class CampaignContentController : BaseController
    {
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

        private void ProcessMarkup(CampaignContentZIP campaignContentZip)
        {
            StaticContentItem zipItem = GetZipItem(campaignContentZip);

            CampaignContentMarkup campaignContentMarkup;
            HtmlHolder htmlMarkup;

            SiteConfiguration.CacheProvider.TryGet<CampaignContentMarkup>(campaignContentZip.Id, "CampaignContent", out campaignContentMarkup);
            if (campaignContentMarkup == null || campaignContentMarkup.LastModified < zipItem.LastModified)
            {
                htmlMarkup = ExtractZip(zipItem, GetBaseDir(campaignContentZip));
                campaignContentMarkup = new CampaignContentMarkup { HtmlMarkup = htmlMarkup, LastModified = zipItem.LastModified };
                SiteConfiguration.CacheProvider.Store<CampaignContentMarkup>(campaignContentZip.Id, "CampaignContent", campaignContentMarkup);
            }
            else
            {
                htmlMarkup = campaignContentMarkup.HtmlMarkup;
            }

            // Throw exception if main HTML is not found

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

            string assetBaseDir = this.GetAssetBaseDir(campaignContentZip);

            // Process assets
            //
            this.ProcessAssetLinks(htmlDoc, assetBaseDir, "href");
            this.ProcessAssetLinks(htmlDoc, assetBaseDir, "src");

            // Insert header markup (JS, CSS etc)
            //
            if ( htmlMarkup.HeaderHtml != null )
            {
                var headerDoc = NSoupClient.Parse("<body>" + htmlMarkup.HeaderHtml + "</body>");
                this.ProcessAssetLinks(headerDoc, assetBaseDir, "src");
                PluggableMarkup.RegisterMarkup("top-js", headerDoc.Body.Html());
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

        private void ProcessAssetLinks(Document htmlDoc, String assetBaseDir, String attributeName)
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

        private StaticContentItem GetZipItem(CampaignContentZIP campaignContentZip)
        {
            return SiteConfiguration.ContentProvider.GetStaticContentItem(campaignContentZip.Url, WebRequestContext.Localization);
        }

        private string GetBaseDir(CampaignContentZIP campaignContentZip)
        {
            // TODO: Have the asset base configurable
            return HttpContext.Server.MapPath("~/campaign-content/assets/" + WebRequestContext.Localization.LocalizationId + "/" + campaignContentZip.Id);
        }

        private String GetAssetBaseDir(CampaignContentZIP campaignContentZip)
        {
            return "/campaign-content/assets/" + WebRequestContext.Localization.LocalizationId + "/" + campaignContentZip.Id;
        }

        private HtmlHolder ExtractZip(StaticContentItem zipItem, String directory)
        {
            HtmlHolder htmlHolder = new HtmlHolder();

            if ( Directory.Exists(directory) )
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
           
            using (ZipArchive archive = new ZipArchive(zipItem.GetContentStream()))
            {
                archive.ExtractToDirectory(directory);
            }

            // Main HTML
            //
            string mainHtmlFile = directory + "/index.html";
            Log.Info("Main HTML: " + mainHtmlFile);
            if (System.IO.File.Exists(mainHtmlFile))
            {
                htmlHolder.MainHtml = System.IO.File.ReadAllText(mainHtmlFile);   
            }

            // Header HTML
            //
            string headerHtmlFile = directory + "/header.html";
            if ( System.IO.File.Exists(headerHtmlFile))
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
    }

    internal class CampaignContentMarkup
    {
        internal HtmlHolder HtmlMarkup { get; set; }
        internal DateTime LastModified { get; set; }
    }

    internal class HtmlHolder
    {
        internal string HeaderHtml { get; set; }
        internal string MainHtml { get; set; }
        internal string FooterHtml { get; set; }
    }
}