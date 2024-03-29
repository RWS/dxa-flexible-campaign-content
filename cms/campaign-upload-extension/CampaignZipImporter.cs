﻿using HtmlAgilityPack;
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
using System.Linq;
using System.Security;

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

            if ( component.ComponentType == ComponentType.Multimedia && component.MetadataSchema.Title.Equals("Campaign Content ZIP") && !component.BinaryContent.Filename.Contains(".Processed") )
            {
                ItemFields content = new ItemFields(component.Metadata, component.MetadataSchema);
                EmbeddedSchemaField taggedContentList = (EmbeddedSchemaField)content["taggedContent"];
                EmbeddedSchemaField taggedImageList = (EmbeddedSchemaField)content["taggedImages"];
                EmbeddedSchemaField taggedPropertyList = (EmbeddedSchemaField)content["taggedProperties"];
                EmbeddedSchemaField taggedLinkList = (EmbeddedSchemaField)content["taggedLinks"];

                var orgItem = component.OrganizationalItem;

                /* TODO: Can we control this via a setting? We could upload an optional add-on configuration
                if ( taggedContentList.Values.Count > 0 || taggedImageList.Values.Count > 0 )
                {
                    // Just do the extraction of content fields from the HTML the first time
                    //
                    return;
                }
                */
                
                // Extract ZIP and find the index.html
                //
                var zipFilename = Path.GetTempPath() + "\\CampaignContent_" + component.Id.ItemId + "_" + DateTime.Now.ToFileTime() + ".zip";
                Logger.Write("Extracting Campaign ZIP: " + zipFilename, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
                using (FileStream fs = File.Create(zipFilename))
                {
                    component.BinaryContent.WriteToStream(fs);
                }

                string html = null;
                using (ZipArchive archive = ZipFile.Open(zipFilename, ZipArchiveMode.Update))
                {
                    ZipArchiveEntry entry = archive.GetEntry("index.html");
                    if (entry == null)
                    {
                        throw new Exception("Missing index.html in the campaign ZIP!");
                    }
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
                        ProcessLinks(htmlDoc, taggedLinkList);

                        component.Metadata = content.ToXml();

                        // Mark the ZIP file processed. This avoid processing the ZIP file each time there is change in the content section.
                        //
                        var filename = component.BinaryContent.Filename;
                        int dotIndex = filename.LastIndexOf(".");
                        if (dotIndex == -1)
                        {
                            filename += ".Processed";
                        }
                        else
                        {
                            filename = filename.Insert(dotIndex, ".Processed");
                        }
                        component.BinaryContent.Filename = filename;

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

                var contentName = node.Attributes["data-content-name"].Value;
                if (!IsEntryAlreadyDefined(contentName, taggedContentList))
                {
                    //Logger.Write("Adding content with name: " + contentName, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
                    var taggedContentXml = new StringBuilder();
                    taggedContentXml.Append("<TaggedContent><name>");
                    taggedContentXml.Append(contentName);
                    taggedContentXml.Append("</name><content>");
                    taggedContentXml.Append(node.InnerHtml);
                    taggedContentXml.Append("</content></TaggedContent>");
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(taggedContentXml.ToString());
                    ItemFields taggedContent = new ItemFields(xmlDoc.DocumentElement, taggedContentSchema);
                    taggedContentList.Values.Add(taggedContent);
                }
             
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
            MultimediaLinkFieldDefinition mmLinkFieldDef = (MultimediaLinkFieldDefinition) taggedImageSchemaFields.Fields.Where(field => field.Name.Equals("image")).First();
            Schema imageSchema = mmLinkFieldDef.AllowedTargetSchemas[0];
            Folder imageFolder = null;

            var taggedImageNames = new List<string>();
            var foundImages = new Dictionary<string, Component>();
            var imageFolderWebDavUrl = parentFolder.WebDavUrl + "/" + componentTitle + " Images";

            foreach (var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-image-name]"))
            {
                var imageUrl = node.Attributes["src"];

                //Logger.Write("Processing image tag...", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
              
                var taggedImageName = node.Attributes["data-image-name"];
                if (imageUrl != null && taggedImageName != null && !taggedImageNames.Contains(taggedImageName.Value) && !IsEntryAlreadyDefined(taggedImageName.Value, taggedImageList))
                {
                    //Logger.Write("Adding image with name: " + taggedImageName.Value, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);

                    if (imageUrl != null && !imageUrl.Value.StartsWith("http"))
                    {
                        if (imageFolder == null)
                        {
                            
                            if (parentFolder.Session.IsExistingObject(imageFolderWebDavUrl))
                            {
                                imageFolder = (Folder) parentFolder.Session.GetObject(imageFolderWebDavUrl);
                            }
                            else
                            {
                                // Create folder
                                //
                                imageFolder = new Folder(parentFolder.Session, parentFolder.Id);
                                imageFolder.Title = componentTitle + " Images";
                                imageFolder.Save();
                            }
                        }
                    }

                    // If an absolute image URL
                    //
                    else if (imageUrl != null && imageUrl.Value.StartsWith("http"))
                    {
                        var url = imageUrl.Value;
                        string parameters = null;
                        if (url.Contains("?"))
                        {
                            var parts = url.Split(new char[] { '?' }, StringSplitOptions.RemoveEmptyEntries);
                            url = parts[0];
                            parameters = parts[1];
                        }
                        var taggedImageXml = new StringBuilder();
                        taggedImageXml.Append("<TaggedImage xmlns:xlink=\"http://www.w3.org/1999/xlink\"><name>");
                        taggedImageXml.Append(taggedImageName.Value);
                        taggedImageXml.Append("</name><imageUrl>");
                        taggedImageXml.Append(SecurityElement.Escape(url));
                        taggedImageXml.Append("</imageUrl>");
                        if (parameters != null)
                        {
                            taggedImageXml.Append("<parameters>");
                            taggedImageXml.Append(SecurityElement.Escape(parameters));
                            taggedImageXml.Append("</parameters>");
                        }
                        taggedImageXml.Append("</TaggedImage>");

                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(taggedImageXml.ToString());
                        ItemFields taggedImage = new ItemFields(xmlDoc.DocumentElement, taggedImageSchema);
                        taggedImageList.Values.Add(taggedImage);
                        taggedImageNames.Add(taggedImageName.Value);
                        continue;  
                    }
                    ZipArchiveEntry imageEntry = archive.GetEntry(imageUrl.Value);
                    if (imageEntry != null)
                    {
                        Component imageComponent;
                        if (foundImages.TryGetValue(imageUrl.Value, out imageComponent) == false)
                        {
                            var imageName = Path.GetFileName(imageUrl.Value);
                            var imageWebDavUri = imageFolderWebDavUrl + "/" + imageName;
                            if (parentFolder.Session.IsExistingObject(imageWebDavUri))
                            {
                                imageComponent = (Component) parentFolder.Session.GetObject(imageWebDavUri);
                            }
                            else
                            {
                                imageComponent = new Component(parentFolder.Session, imageFolder.Id);
                                var metadataXml = new XmlDocument();
                                metadataXml.LoadXml("<Metadata xmlns=\"" + imageSchema.NamespaceUri + "\"/>");
                                imageComponent.Schema = imageSchema;
                                imageComponent.Metadata = metadataXml.DocumentElement;

                                var extension = Path.GetExtension(imageUrl.Value);
                                imageComponent.Title = imageName.Replace(extension, ""); // Set title without extension
                                extension = extension.ToLower().Replace(".", "");

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
                            }
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
                    if (!IsEntryAlreadyDefined(propertyName.Value, taggedPropertyList))
                    {
                        var propertyTarget = node.Attributes["data-property-target" + indexSuffix];
                        if (propertyTarget == null)
                        {
                            Logger.Write("Missing property target for property '" + propertyName.Value + "'. Skpping property...", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Warning);
                            continue;
                        }

                        //Logger.Write("Adding property with name: " + propertyName.Value, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);

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
                    }

                    index++;
                    indexSuffix = "-" + index;
                }

            }
        }

        /// <summary>
        /// Process links
        /// </summary>
        /// <param name="htmlDoc"></param>
        /// <param name="taggedLinkList"></param>
        private static void ProcessLinks(HtmlDocument htmlDoc, EmbeddedSchemaField taggedLinkList)
        {
            Schema taggedLinkSchema = ((EmbeddedSchemaFieldDefinition)taggedLinkList.Definition).EmbeddedSchema;
            foreach (var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-link-name]"))
            {
                var linkName = node.Attributes["data-link-name"].Value;
                if (!IsEntryAlreadyDefined(linkName, taggedLinkList))
                {
                    //Logger.Write("Adding link with name: " + linkName, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
                    var taggedLinkXml = new StringBuilder();
                    taggedLinkXml.Append("<TaggedLink><name>");
                    taggedLinkXml.Append(linkName);
                    taggedLinkXml.Append("</name>");
                    var linkValue = node.Attributes["href"];
                    if (linkValue != null)
                    {
                        taggedLinkXml.Append("<url>");
                        taggedLinkXml.Append(linkValue.Value);
                        taggedLinkXml.Append("</url>");
                    }
                    taggedLinkXml.Append("</TaggedLink>");
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(taggedLinkXml.ToString());
                    ItemFields taggedLinks = new ItemFields(xmlDoc.DocumentElement, taggedLinkSchema);
                    taggedLinkList.Values.Add(taggedLinks);
                }
            }
        }


        /// <summary>
        /// Is entry already defined
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="esField"></param>
        /// <returns></returns>
        private static bool IsEntryAlreadyDefined(string entryName, EmbeddedSchemaField esField)
        {
            foreach (var value in esField.Values)
            {
                if (value.Contains("name"))
                {
                    var name = value["name"].ToString();
                    if (name.Equals(entryName))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
    
}
