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

    private static final Logger LOG = LoggerFactory.getLogger(CampaignContentController.class);

    @Autowired
    private ContentProvider contentProvider;

    @Autowired
    private WebRequestContext webRequestContext;

    @Autowired
    private WebApplicationContext webApplicationContext;

    @Autowired
    private PluggableMarkupRegistry pluggableMarkupRegistry;

    private Map<String, FreeFormatMarkup> cachedMarkup = new HashMap<>();

    static class FreeFormatMarkup {

        HtmlHolder htmlMarkup;
        long lastModified;

        FreeFormatMarkup(HtmlHolder htmlMarkup, long lastModified) {
            this.htmlMarkup = htmlMarkup;
            this.lastModified  = lastModified;
        }
    }

    static class HtmlHolder {
        String headerHtml;
        String mainHtml;
        String footerHtml;
    }

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

    private void getProcessedMarkup(CampaignContentZIP freeFormatContent) throws ContentProviderException {

        Localization localization = this.webRequestContext.getLocalization();
        StaticContentItem zipItem = this.getZipItem(freeFormatContent, localization);
        FreeFormatMarkup markup = this.cachedMarkup.get(freeFormatContent.getId());
        if( markup == null || markup.lastModified < zipItem.getLastModified() ) {
            try {
                HtmlHolder htmlMarkup = extractZip(zipItem, this.getBaseDir(freeFormatContent, localization));
                markup = new FreeFormatMarkup(htmlMarkup, zipItem.getLastModified());
                this.cachedMarkup.put(freeFormatContent.getId(), markup);
            }
            catch ( IOException e ) {
                throw new ContentProviderException("Could not extract free format ZIP file.", e);
            }
        }
        String processedMarkup = this.processFreeFormatMarkup(freeFormatContent, markup.htmlMarkup, this.getAssetBaseDir(freeFormatContent, localization));
        freeFormatContent.setProcessedContent(new RichTextFragmentImpl(processedMarkup).getHtml());
    }

    private String processFreeFormatMarkup(CampaignContentZIP freeFormatContent, HtmlHolder inputHtmlMarkup, String assetBaseDir) {
        Document htmlDoc = Jsoup.parse("<body>" + inputHtmlMarkup.mainHtml + "</body>");

        // Inject content into placeholders in the markup
        //
        if ( freeFormatContent.getTaggedContent() != null ) {

            int index = 1;
            for (val taggedContent : freeFormatContent.getTaggedContent()) {
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

        // Process assets
        //
        this.processAssetLinks(htmlDoc, assetBaseDir, "href");
        this.processAssetLinks(htmlDoc, assetBaseDir, "src");

        // Insert header markup (JS, CSS etc)
        //
        if ( inputHtmlMarkup.headerHtml != null ) {
            Document headerDoc = Jsoup.parse("<body>" + inputHtmlMarkup.headerHtml + "</body>");
            this.processAssetLinks(headerDoc, assetBaseDir, "src");
            this.pluggableMarkupRegistry.registerContextualPluggableMarkup("top-js", new ParsableHtmlNode(headerDoc.body().html()));
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

    private void processAssetLinks(Document htmlDoc, String assetBaseDir, String attributeName) {
        for ( val element : htmlDoc.body().select("[" + attributeName +"]") ) {
            String assetUrl = element.attr(attributeName);
            if ( !assetUrl.startsWith("/") && !assetUrl.startsWith("http") && !assetUrl.startsWith("#") ) {
                assetUrl = assetBaseDir + "/" + assetUrl;
                element.attr(attributeName, assetUrl);
            }
        }
    }

    private StaticContentItem getZipItem(CampaignContentZIP freeFormatContent, Localization localization) throws ContentProviderException {
        return contentProvider.getStaticContent(freeFormatContent.getUrl(), localization.getId(), localization.getPath());
    }

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

    protected File getBaseDir(CampaignContentZIP freeFormatContent, Localization localization) {

        return new File(StringUtils.join(new String[]{
                webApplicationContext.getServletContext().getRealPath("/"), "system/assets", localization.getId(), "freeformat", freeFormatContent.getId()
        }, File.separator));
    }

    protected String getAssetBaseDir(CampaignContentZIP freeFormatContent, Localization localization) {
        return StringUtils.join(new String[]{ "/system/assets", localization.getId(), "freeformat", freeFormatContent.getId()}, File.separator);
    }
}
