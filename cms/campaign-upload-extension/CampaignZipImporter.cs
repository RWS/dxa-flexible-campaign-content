using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Tridion.ContentManager;
using Tridion.ContentManager.ContentManagement;
using Tridion.ContentManager.Extensibility;
using Tridion.ContentManager.Extensibility.Events;
using Tridion.Logging;
using Tridion.ContentManager.ContentManagement.Fields;
using System.Xml;

namespace SDL.Web.Extensions.CampaignUpload
{
    /// <summary>
    /// Campaign ZIP Importer
    /// </summary>
    [TcmExtension("CampaignUpload-Extension")]
    public class CampaignZipImporter : TcmExtension
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public CampaignZipImporter()
        {
            Logger.Write("Initializing Campaign ZIP Importer..", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
            EventSystem.Subscribe<Component, SaveEventArgs>(OnComponentSave, EventPhases.Processed);
        }

        /// <summary>
        /// On Component Save
        /// </summary>
        /// <param name="component"></param>
        /// <param name="args"></param>
        /// <param name="phase"></param>
        public static void OnComponentSave(Component component, SaveEventArgs args, EventPhases phase)
        {
            // TODO: Have a better way of detecting the campaign content zip
            if ( component.ComponentType == ComponentType.Multimedia && component.MetadataSchema.Title.Equals("Campaign Content ZIP") )
            {
                ItemFields content = new ItemFields(component.Metadata, component.MetadataSchema);
                EmbeddedSchemaField taggedContentList = (EmbeddedSchemaField)content["taggedContent"];

                if ( taggedContentList.Values.Count > 0 )
                {
                    // Just do the extraction of content fields from the HTML the first time
                    //
                    return;
                }

                // Extract ZIP and find the index.html
                //
                var zipFilename = Path.GetTempPath() + "\\CampaignContent_" + component.Id.ItemId + "_" + DateTime.Now.ToFileTime() + ".zip";
                Logger.Write("Extracting ZIP: " + zipFilename, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
                using (FileStream fs = File.Create(zipFilename))
                {
                    component.BinaryContent.WriteToStream(fs);
                }

                string html = null;
                using (ZipArchive archive = ZipFile.Open(zipFilename, ZipArchiveMode.Update))
                {
                    ZipArchiveEntry entry = archive.GetEntry("index.html");
                   
                    using (StreamReader reader = new StreamReader(entry.Open()))
                    {
                        html = reader.ReadToEnd();
                    }
                }

                if (html != null)
                {
                    // Parse the HTML and find all content items
                    //
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(html);
                    htmlDoc.OptionOutputAsXml = true;
           
                    Schema taggedContentSchema = ( (EmbeddedSchemaFieldDefinition) taggedContentList.Definition).EmbeddedSchema;
                 
                    foreach ( var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-content-name]") )
                    {
                        // Add XHTML namespace to all elements in the content markup
                        //
                        foreach ( var element in node.QuerySelectorAll("*") )
                        {
                            element.SetAttributeValue("xmlns", "http://www.w3.org/1999/xhtml");
                        } 

                        var taggedContentXml = new StringBuilder();
                        taggedContentXml.Append("<TaggedContent><name>");
                        taggedContentXml.Append(node.Attributes["data-content-name"].Value);
                        taggedContentXml.Append("</name><content>");
                 
                        taggedContentXml.Append(node.InnerHtml);
                        taggedContentXml.Append("</content></TaggedContent>");
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(taggedContentXml.ToString());
                        ItemFields taggedContent = new ItemFields(xmlDoc.DocumentElement, taggedContentSchema);
                        taggedContentList.Values.Add(taggedContent);
                    }

                    component.Metadata = content.ToXml();
                    component.Save();
                    File.Delete(zipFilename);
                }
            }

        }
    }
}
