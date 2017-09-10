package com.sdl.dxa.modules.campaigncontent.controller;

import com.sdl.dxa.modules.campaigncontent.model.CampaignContentZIP;
import com.sdl.webapp.common.api.WebRequestContext;
import com.sdl.webapp.common.api.content.ContentProvider;
import com.sdl.webapp.common.api.content.ContentProviderException;
import com.sdl.webapp.common.api.content.StaticContentItem;
import com.sdl.webapp.common.api.localization.Localization;
import com.sdl.webapp.common.api.model.EntityModel;
import com.sdl.webapp.common.api.model.MvcData;
import com.sdl.webapp.common.api.model.RichTextFragmentImpl;
import com.sdl.webapp.common.controller.BaseController;
import com.sdl.webapp.common.controller.exception.NotFoundException;
import com.sdl.webapp.common.markup.PluggableMarkupRegistry;
import com.sdl.webapp.common.markup.html.ParsableHtmlNode;
import lombok.val;
import org.apache.commons.lang3.StringUtils;
import org.jsoup.Jsoup;
import org.jsoup.nodes.Document;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;
import org.springframework.web.context.WebApplicationContext;

import javax.servlet.http.HttpServletRequest;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/**
 * Campaign Content Controller
 *
 * @author nic
 */
@Controller
@RequestMapping("/system/mvc/CampaignContent/CampaignContent")
public class CampaignContentController extends BaseController {

    // TODO: Create a proxy to all assets to build better URLs and align to DXA standards

    private static final Logger LOG = LoggerFactory.getLogger(CampaignContentController.class);

    @Autowired
    private ContentProvider contentProvider;

    @Autowired
    private WebRequestContext webRequestContext;

    @Autowired
    private WebApplicationContext webApplicationContext;

    @Autowired
    private PluggableMarkupRegistry pluggableMarkupRegistry;

    private Map<String, CampaignContentMarkup> cachedMarkup = new HashMap<>();

    /**
     * Holder class for campaign content
     */
    static public class CampaignContentMarkup {

        HtmlHolder htmlMarkup;
        long lastModified;

        CampaignContentMarkup(HtmlHolder htmlMarkup, long lastModified) {
            this.htmlMarkup = htmlMarkup;
            this.lastModified  = lastModified;
        }
    }

    /**
     * Holder class for the different HTML fragments in a campaign
     */
    static public class HtmlHolder {
        String headerHtml;
        String mainHtml;
        String footerHtml;
    }

    /**
     * Assembly content
     * @param request
     * @param entityId
     * @return
     * @throws ContentProviderException
     */
    @RequestMapping(method = RequestMethod.GET, value = "AssemblyContent/{entityId}")
    public String assemblyContent(HttpServletRequest request, @PathVariable String entityId) throws ContentProviderException {

        CampaignContentZIP entity = (CampaignContentZIP) this.getEntityFromRequest(request, entityId);
        this.getProcessedMarkup(entity);
        final MvcData mvcData = entity.getMvcData();
        request.setAttribute("entity", entity);
        return resolveView(mvcData, "Entity", request);
    }

    /**
     * Get the entity from the request
     * @param request
     * @param entityId
     * @return
     */
    protected EntityModel getEntityFromRequest(HttpServletRequest request, String entityId) {
        final EntityModel entity = (EntityModel) request.getAttribute("_entity_");
        if (entity == null) {
            throw new NotFoundException("Entity not found in request: " + entityId);
        }
        return entity;
    }

    /**
     * Get processed markup
     * @param campaignContentZip
     * @throws ContentProviderException
     */
    protected void getProcessedMarkup(CampaignContentZIP campaignContentZip) throws ContentProviderException {

        Localization localization = this.webRequestContext.getLocalization();
        StaticContentItem zipItem = this.getZipItem(campaignContentZip, localization);

        String cacheKey = campaignContentZip.getId() + "-" + localization.getId();
        CampaignContentMarkup markup = this.cachedMarkup.get(cacheKey);
        File baseDir = this.getBaseDir(campaignContentZip, localization);
        
        if( markup == null || !baseDir.exists() || !this.directoryHasFiles(baseDir) ) {
            try {
                HtmlHolder htmlMarkup = extractZip(zipItem, baseDir);
                markup = new CampaignContentMarkup(htmlMarkup, zipItem.getLastModified());
                this.cachedMarkup.put(cacheKey, markup);
            }
            catch ( IOException e ) {
                throw new ContentProviderException("Could not extract free format ZIP file.", e);
            }
        }
        String processedMarkup = this.processMarkup(campaignContentZip, markup.htmlMarkup, this.getAssetBaseDir(campaignContentZip, localization));
        campaignContentZip.setProcessedContent(new RichTextFragmentImpl(processedMarkup).getHtml());
    }

    /**
     * Process markup
     * @param campaignContentZip
     * @param inputHtmlMarkup
     * @param assetBaseDir
     * @return
     */
    protected String processMarkup(CampaignContentZIP campaignContentZip, HtmlHolder inputHtmlMarkup, String assetBaseDir) {
        Document htmlDoc = Jsoup.parse("<body>" + inputHtmlMarkup.mainHtml + "</body>");

        // Inject content into placeholders in the markup
        //
        if ( campaignContentZip.getTaggedContent() != null ) {

            int index = 1;
            for (val taggedContent : campaignContentZip.getTaggedContent()) {
                for (val element : htmlDoc.body().select("[data-content-name=" + taggedContent.getName() + "]")) {
                    String contentMarkup =
                            "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedContent[" +
                            index +
                            "]/custom:content[1]\"} -->" +
                            taggedContent.getContent().toString();
                    element.html(contentMarkup);
                }
                index++;
            }
        }

        // Inject tagged properties
        //
        if ( campaignContentZip.getTaggedProperties() != null ) {
            for (val taggedProperty : campaignContentZip.getTaggedProperties() ) {
                for (val element : htmlDoc.body().select("[data-property-name=" + taggedProperty.getName() + "]")) {
                    element.attr(taggedProperty.getTarget(), taggedProperty.getValue());
                }
            }
        }

        // Process assets
        //
        this.processAssetLinks(htmlDoc, assetBaseDir, "href");
        this.processAssetLinks(htmlDoc, assetBaseDir, "src");

        // Inject tagged images
        //
        if ( campaignContentZip.getTaggedImages() != null ) {
            int index = 1;
            for (val taggedImage : campaignContentZip.getTaggedImages()) {
                for (val element : htmlDoc.body().select("[data-image-name=" + taggedImage.getName() + "]")) {
                    String xpmMarkup =
                            "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedImages[" +
                            index +
                            "]/custom:image[1]\"} -->";

                    element.attr("src", taggedImage.getImage().getUrl()); // Right now always assume the data tag is only used in img tags
                    element.before(xpmMarkup);
                }
                index++;
            }
        }

        // Insert header markup (JS, CSS etc)
        //
        if ( inputHtmlMarkup.headerHtml != null ) {
            Document headerDoc = Jsoup.parse("<body>" + inputHtmlMarkup.headerHtml + "</body>");
            this.processAssetLinks(headerDoc, assetBaseDir, "href");
            this.processAssetLinks(headerDoc, assetBaseDir, "src");

            // As contextual pluggable markup does not work for header content right now, we have to do the actual
            // include the CSS in the top of the campaign content instead
            //
            //this.pluggableMarkupRegistry.registerContextualPluggableMarkup("css", new ParsableHtmlNode(headerDoc.body().html()));

            htmlDoc.body().prependChild(headerDoc.body());
        }

        // Insert footer markup (JS etc)
        //
        if ( inputHtmlMarkup.footerHtml != null ) {
            Document footerDoc = Jsoup.parse("<body>" + inputHtmlMarkup.footerHtml + "</body>");
            this.processAssetLinks(footerDoc, assetBaseDir, "src");
            this.pluggableMarkupRegistry.registerContextualPluggableMarkup("bottom-js", new ParsableHtmlNode(footerDoc.body().html()));
        }

        return htmlDoc.body().html();
    }

    /**
     * Process asset links
     * @param htmlDoc
     * @param assetBaseDir
     * @param attributeName
     */
    protected void processAssetLinks(Document htmlDoc, String assetBaseDir, String attributeName) {
        for ( val element : htmlDoc.body().select("[" + attributeName +"]") ) {
            String assetUrl = element.attr(attributeName);
            if ( !assetUrl.startsWith("/") && !assetUrl.startsWith("http") && !assetUrl.startsWith("#") ) {
                assetUrl = assetBaseDir + "/" + assetUrl;
                element.attr(attributeName, assetUrl);
            }
        }
    }

    /**
     * Get ZIP item
     * @param campaignContentZip
     * @param localization
     * @return
     * @throws ContentProviderException
     */
    protected StaticContentItem getZipItem(CampaignContentZIP campaignContentZip, Localization localization) throws ContentProviderException {
        return contentProvider.getStaticContent(campaignContentZip.getUrl(), localization.getId(), localization.getPath());
    }

    /**
     * Extract ZIP
     * @param zipItem
     * @param directory
     * @return
     * @throws IOException
     */
    protected HtmlHolder extractZip(StaticContentItem zipItem, File directory) throws IOException {

        ByteArrayOutputStream htmlMarkup = new ByteArrayOutputStream();
        ByteArrayOutputStream headerMarkup = new ByteArrayOutputStream();
        ByteArrayOutputStream footerMarkup = new ByteArrayOutputStream();

        byte[] buffer = new byte[1024];

        directory.mkdir();

        // Get the zip file content
        //
        ZipInputStream zis =
                new ZipInputStream(zipItem.getContent());

        // Get the zipped file list entry
        //
        ZipEntry ze = zis.getNextEntry();

        while (ze!=null) {
            String fileName = ze.getName();
            File newFile = new File(directory + File.separator + fileName);

            // Create all non exists folders
            // else you will hit FileNotFoundException for compressed folder
            //
            if (ze.isDirectory()) {
                newFile.mkdirs();
            }
            else {
                newFile.getParentFile().mkdirs();
                newFile.createNewFile();
                FileOutputStream fos = new FileOutputStream(newFile);
                int len;
                while ((len = zis.read(buffer)) > 0) {
                    fos.write(buffer, 0, len);
                    if (fileName.equals("index.html") ) {
                        htmlMarkup.write(buffer, 0, len);
                    }
                    else if (fileName.equals("header.html") ) {
                        headerMarkup.write(buffer, 0, len);
                    }
                    else if (fileName.equals("footer.html") ) {
                        footerMarkup.write(buffer, 0, len);
                    }

                }
                fos.close();
            }
            ze = zis.getNextEntry();
        }
        zis.closeEntry();
        zis.close();

        HtmlHolder htmlHolder = new HtmlHolder();
        if ( htmlMarkup.size() > 0 ) {
            htmlHolder.mainHtml = new String(htmlMarkup.toByteArray());
        }
        if ( headerMarkup.size() > 0 ) {
            htmlHolder.headerHtml = new String(headerMarkup.toByteArray());
        }
        if ( footerMarkup.size() > 0 ) {
            htmlHolder.footerHtml = new String(footerMarkup.toByteArray());
        }
        return htmlHolder;
    }

    /**
     * Check if a specific directory has files
     * @param directory
     * @return
     */
    protected boolean directoryHasFiles(File directory) {
        String[] files = directory.list();
        return files != null && files.length > 0;

    }

    /**
     * Get base directory for the specified campaign content
     * @param campaignContentZip
     * @param localization
     * @return base directory
     */
    protected File getBaseDir(CampaignContentZIP campaignContentZip, Localization localization) {

        // TODO: Align with the C# version and use the same path pattern

        return new File(StringUtils.join(new String[]{
                webApplicationContext.getServletContext().getRealPath("/"), "system/assets", localization.getId(), "campaign-content", campaignContentZip.getId()
        }, File.separator));
    }

    /**
     * Get asset base directory for the specified campaign content
     * @param campaignContentZip
     * @param localization
     * @return asset base directory
     */
    protected String getAssetBaseDir(CampaignContentZIP campaignContentZip, Localization localization) {
        return StringUtils.join(new String[]{ "/system/assets", localization.getId(), "campaign-content", campaignContentZip.getId()}, File.separator);
    }
}
