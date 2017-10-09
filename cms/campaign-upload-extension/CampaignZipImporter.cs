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
using System.Xml.Linq;
using System.Collections.Generic;

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
            // TODO: Refactor into smaller methods

            if ( component.ComponentType == ComponentType.Multimedia && component.MetadataSchema.Title.Equals("Campaign Content ZIP") )
            {
                ItemFields content = new ItemFields(component.Metadata, component.MetadataSchema);
                EmbeddedSchemaField taggedContentList = (EmbeddedSchemaField)content["taggedContent"];
                EmbeddedSchemaField taggedImageList = (EmbeddedSchemaField)content["taggedImages"];
                EmbeddedSchemaField taggedPropertyList = (EmbeddedSchemaField)content["taggedProperties"];

                var orgItem = component.OrganizationalItem;

                if ( taggedContentList.Values.Count > 0 || taggedImageList.Values.Count > 0 )
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

                    if (html != null)
                    {
                        // Parse the HTML and find all content items
                        //
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(html);
                        htmlDoc.OptionOutputAsXml = true;

                        ProcessContent(htmlDoc, taggedContentList);
                        ProcessImages(htmlDoc, taggedImageList, orgItem, component.Title, archive);
                        ProcessProperties(htmlDoc, taggedPropertyList);

                        component.Metadata = content.ToXml();
                        component.Save();
                    }
                }
                File.Delete(zipFilename);
            }

        }
        
        /// <summary>
        /// Process content
        /// </summary>
        /// <param name="htmlDoc"></param>
        /// <param name="taggedContentList"></param>
        private static void ProcessContent(HtmlDocument htmlDoc, EmbeddedSchemaField taggedContentList)
        {
            Schema taggedContentSchema = ((EmbeddedSchemaFieldDefinition)taggedContentList.Definition).EmbeddedSchema;

            foreach (var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-content-name]"))
            {
                // Add XHTML namespace to all elements in the content markup
                //
                foreach (var element in node.QuerySelectorAll("*"))
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

        }

        /// <summary>
        /// Process images
        /// </summary>
        /// <param name="htmlDoc"></param>
        /// <param name="taggedImageList"></param>
        /// <param name="parentFolder"></param>
        /// <param name="componentTitle"></param>
        /// <param name="archive"></param>
        private static void ProcessImages(HtmlDocument htmlDoc, EmbeddedSchemaField taggedImageList, OrganizationalItem parentFolder, String componentTitle, ZipArchive archive)
        {
            EmbeddedSchemaFieldDefinition taggedImageField = (EmbeddedSchemaFieldDefinition)taggedImageList.Definition;
            Schema taggedImageSchema = taggedImageField.EmbeddedSchema;
            SchemaFields taggedImageSchemaFields = new SchemaFields(taggedImageSchema);
            MultimediaLinkFieldDefinition mmLinkFieldDef = (MultimediaLinkFieldDefinition)taggedImageSchemaFields.Fields[1];
            Schema imageSchema = mmLinkFieldDef.AllowedTargetSchemas[0];
            Folder imageFolder = null;

            var taggedImageNames = new List<string>();
            var foundImages = new Dictionary<string, Component>();

            foreach (var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-image-name]"))
            {
                if (imageFolder == null)
                {
                    imageFolder = new Folder(parentFolder.Session, parentFolder.Id);
                    imageFolder.Title = componentTitle + " " + "Images";
                    imageFolder.Save();
                }

                //Logger.Write("Processing image tag...", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);

                var imageUrl = node.Attributes["src"];
                var taggedImageName = node.Attributes["data-image-name"];
                if (imageUrl != null && taggedImageName != null && !taggedImageNames.Contains(taggedImageName.Value) )
                {
                    
                    ZipArchiveEntry imageEntry = archive.GetEntry(imageUrl.Value);
                    if (imageEntry != null)
                    {
                        Component imageComponent;
                        if (foundImages.TryGetValue(imageUrl.Value, out imageComponent) == false)
                        {
                            imageComponent = new Component(parentFolder.Session, imageFolder.Id);
                            var imageName = Path.GetFileName(imageUrl.Value);
                            var metadataXml = new XmlDocument();
                            metadataXml.LoadXml("<Metadata xmlns=\"" + imageSchema.NamespaceUri + "\"/>");
                            imageComponent.Schema = imageSchema;
                            imageComponent.Metadata = metadataXml.DocumentElement;
                            imageComponent.Title = imageName;

                            var extension = Path.GetExtension(imageUrl.Value).ToLower();
                            bool foundMMType = false;
                            foreach (var mmType in imageSchema.AllowedMultimediaTypes)
                            {
                                if (mmType.FileExtensions.Contains(extension))
                                {
                                    imageComponent.BinaryContent.MultimediaType = mmType;
                                    foundMMType = true;
                                    break;
                                }
                            }
                            if (!foundMMType)
                            {
                                Logger.Write("Could not find multimedia type for image extension: " + extension, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Error);
                            }

                            imageComponent.BinaryContent.UploadFromStream = imageEntry.Open();
                            imageComponent.BinaryContent.Filename = imageName;
                            imageComponent.Save(true);
                            foundImages.Add(imageUrl.Value, imageComponent);
                        }
                        var taggedImageXml = new StringBuilder();
                        taggedImageXml.Append("<TaggedImage xmlns:xlink=\"http://www.w3.org/1999/xlink\"><name>");
                        taggedImageXml.Append(taggedImageName.Value);
                        taggedImageXml.Append("</name><image xlink:type=\"simple\" xlink:href=\"");
                        taggedImageXml.Append(imageComponent.Id);
                        taggedImageXml.Append("\" xlink:title=\"");
                        taggedImageXml.Append(imageComponent.Title);
                        taggedImageXml.Append("\" /></TaggedImage>");

                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(taggedImageXml.ToString());
                        ItemFields taggedImage = new ItemFields(xmlDoc.DocumentElement, taggedImageSchema);
                        taggedImageList.Values.Add(taggedImage);
                        taggedImageNames.Add(taggedImageName.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Process properties
        /// </summary>
        /// <param name="htmlDoc"></param>
        /// <param name="taggedPropertyList"></param>
        private static void ProcessProperties(HtmlDocument htmlDoc, EmbeddedSchemaField taggedPropertyList)
        {
            Schema taggedPropertySchema = ((EmbeddedSchemaFieldDefinition)taggedPropertyList.Definition).EmbeddedSchema;

            foreach (var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-property-name]"))
            {
                //Logger.Write("Processing property tag...", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);

                int index = 1;
                string indexSuffix = "";
                while (true)
                {
                    if (!node.Attributes.Contains("data-property-name" + indexSuffix) ||
                         !node.Attributes.Contains("data-property-target" + indexSuffix))
                    {
                        break;
                    }
                    var propertyName = node.Attributes["data-property-name" + indexSuffix];
                    var propertyTarget = node.Attributes["data-property-target" + indexSuffix];
                    if (propertyTarget == null)
                    {
                        Logger.Write("Missing property target for property '" + propertyName.Value + "'. Skpping property...", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Warning);
                        continue;
                    }
                    var propertyValue = node.Attributes[propertyTarget.Value];

                    var taggedPropertyXml = new StringBuilder();
                    taggedPropertyXml.Append("<TaggedProperty xmlns:xlink=\"http://www.w3.org/1999/xlink\"><name>");
                    taggedPropertyXml.Append(propertyName.Value);
                    taggedPropertyXml.Append("</name><value>");
                    taggedPropertyXml.Append(propertyValue.Value);
                    taggedPropertyXml.Append("</value>");
                    if (index > 1)
                    {
                        taggedPropertyXml.Append("<index>");
                        taggedPropertyXml.Append(index);
                        taggedPropertyXml.Append("</index>");
                    }
                    taggedPropertyXml.Append("<target>");
                    taggedPropertyXml.Append(propertyTarget.Value);
                    taggedPropertyXml.Append("</target></TaggedProperty>");

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(taggedPropertyXml.ToString());
                    ItemFields taggedProperty = new ItemFields(xmlDoc.DocumentElement, taggedPropertySchema);
                    taggedPropertyList.Values.Add(taggedProperty);

                    index++;
                    indexSuffix = "-" + index;
                }

            }
        }
    }
}
