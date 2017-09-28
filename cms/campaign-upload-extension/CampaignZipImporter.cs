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
                EmbeddedSchemaField taggedImageList = (EmbeddedSchemaField)content["taggedImages"];
                EmbeddedSchemaField taggedPropertyList = (EmbeddedSchemaField)content["taggedProperties"];

                var orgItem = component.OrganizationalItem;
                var session = component.Session;

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

                        Schema taggedContentSchema = ((EmbeddedSchemaFieldDefinition)taggedContentList.Definition).EmbeddedSchema;
                        EmbeddedSchemaFieldDefinition taggedImageField = (EmbeddedSchemaFieldDefinition) taggedImageList.Definition;
                        Schema taggedImageSchema = taggedImageField.EmbeddedSchema;
                        SchemaFields taggedImageSchemaFields = new SchemaFields(taggedImageSchema);
                        MultimediaLinkFieldDefinition mmLinkFieldDef = (MultimediaLinkFieldDefinition) taggedImageSchemaFields.Fields[1];
                        Schema taggedPropertySchema= ((EmbeddedSchemaFieldDefinition)taggedPropertyList.Definition).EmbeddedSchema;
                        
                        Schema imageSchema = mmLinkFieldDef.AllowedTargetSchemas[0];

                        if (imageSchema != null)
                        {
                            Logger.Write("Image Schema: " + imageSchema, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
                        }
                        //TODO: What to do if image schema is null??

                        // TODO: Refactor into several methods here

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

                        foreach (var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-image-name]"))
                        {
                            Logger.Write("Processing image tag...", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);

                            var imageUrl = node.Attributes["src"];
                            if (imageUrl != null)
                            {
                                Logger.Write("Image URL:" + imageUrl.Value, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
                                ZipArchiveEntry imageEntry = archive.GetEntry(imageUrl.Value);
                                if (imageEntry != null)
                                {
                                    Logger.Write("Image Length: " + imageEntry.Length, "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);
                              
                                    // TODO: Store images in a sub-directory, e.g. 'images'

                                    Component imageComponent = new Component(session, orgItem.Id);

                                    // TODO: Have a better algorithm for the image name
                                    var imageName = component.Title + "-" + imageUrl.Value.Replace("/", "-");
                                    var metadataXml = new XmlDocument();
                                    metadataXml.LoadXml(@"<Metadata xmlns=""http://www.sdl.com/web/schemas/core""/>"); // TODO: Get this namespace from the schema def
                                    imageComponent.Schema = imageSchema;
                                    imageComponent.Metadata = metadataXml.DocumentElement;
                                    imageComponent.Title = imageName;
                                    imageComponent.BinaryContent.MultimediaType = new MultimediaType(new TcmUri("tcm:0-3-65544"), session); // TEMP HARDCODE...
                                    imageComponent.BinaryContent.UploadFromStream = imageEntry.Open();
                                    imageComponent.BinaryContent.Filename = imageName;                   
                                    imageComponent.Save(true);

                                    // TODO: Add error handling if images already exist
                                    var taggedImageXml = new StringBuilder();
                                    taggedImageXml.Append("<TaggedImage xmlns:xlink=\"http://www.w3.org/1999/xlink\"><name>");
                                    taggedImageXml.Append(node.Attributes["data-image-name"].Value);
                                    taggedImageXml.Append("</name><image xlink:type=\"simple\" xlink:href=\"");
                                    taggedImageXml.Append(imageComponent.Id);
                                    taggedImageXml.Append("\" xlink:title=\"");
                                    taggedImageXml.Append(imageComponent.Title);
                                    taggedImageXml.Append("\" /></TaggedImage>");

                                    XmlDocument xmlDoc = new XmlDocument();
                                    xmlDoc.LoadXml(taggedImageXml.ToString());
                                    ItemFields taggedImage = new ItemFields(xmlDoc.DocumentElement, taggedImageSchema);
                                    taggedImageList.Values.Add(taggedImage);
                                }
                            }
                        }

                        foreach (var node in htmlDoc.DocumentNode.QuerySelectorAll("[data-property-name]"))
                        {
                            Logger.Write("Processing property tag...", "CampaignZipImporter", LogCategory.Custom, System.Diagnostics.TraceEventType.Information);

                            int index = 1;
                            string indexSuffix = "";
                            while (true)
                            {
                                if ( ! node.Attributes.Contains("data-property-name" + indexSuffix) || 
                                     ! node.Attributes.Contains("data-property-target" + indexSuffix) )
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
                                if ( index > 1)
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

                        component.Metadata = content.ToXml();
                        component.Save();
                    }
                }
                File.Delete(zipFilename);
            }

        }
    }
}
