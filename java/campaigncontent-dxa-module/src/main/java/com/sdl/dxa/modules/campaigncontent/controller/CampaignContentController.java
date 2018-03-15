package com.sdl.dxa.modules.campaigncontent.controller;

import com.sdl.dxa.modules.campaigncontent.model.CampaignContentZIP;
import com.sdl.dxa.modules.campaigncontent.provider.CampaignAssetProvider;
import com.sdl.dxa.modules.campaigncontent.provider.CampaignContentMarkup;
import com.sdl.webapp.common.api.WebRequestContext;
import com.sdl.webapp.common.api.content.ContentProviderException;
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
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;

import javax.servlet.http.HttpServletRequest;
import java.io.File;

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
    private WebRequestContext webRequestContext;

    @Autowired
    private PluggableMarkupRegistry pluggableMarkupRegistry;

    @Autowired
    private CampaignAssetProvider campaignAssetProvider;

    @Value("${instantcampaign.asset.baseUrl:/assets/campaign}")
    // TODO: Right now this has to be hard coded until DXA supports Spring 3.2+
    private String assetBaseUrl;

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
        CampaignContentMarkup markup = this.campaignAssetProvider.getCampaignContentMarkup(campaignContentZip, localization);
        String processedMarkup = this.processMarkup(campaignContentZip, markup, this.getAssetBaseDir(campaignContentZip, localization));
        campaignContentZip.setProcessedContent(new RichTextFragmentImpl(processedMarkup).getHtml());
    }

    /**
     * Process markup
     * @param campaignContentZip
     * @param markup
     * @param assetBaseDir
     * @return
     */
    protected String processMarkup(CampaignContentZIP campaignContentZip, CampaignContentMarkup markup, String assetBaseDir) {
        Document htmlDoc = Jsoup.parse("<body>" + markup.getMainHtml() + "</body>");

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
                Integer index = taggedProperty.getIndex();
                String indexSuffix = "";
                if ( index != null && index > 1 ) {
                    indexSuffix = "-" + index;
                }
                for (val element : htmlDoc.body().select("[data-property-name" + indexSuffix  + "=" + taggedProperty.getName() + "]")) {
                    element.attr(taggedProperty.getTarget(), taggedProperty.getValue());
                }
            }
        }

        // Inject tagged links
        //
        if ( campaignContentZip.getTaggedLinks() != null ) {
            for (val taggedLink : campaignContentZip.getTaggedLinks() ) {
                for (val element : htmlDoc.body().select("[data-link-name" + "=" + taggedLink.getName() + "]")) {
                    String href = taggedLink.getComponentLink();
                    if ( href == null ) {
                        href = taggedLink.getUrl();
                    }
                    element.attr("href", href);
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
        if ( markup.getHeaderHtml() != null ) {
            Document headerDoc = Jsoup.parse("<body>" + markup.getHeaderHtml() + "</body>");
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
        if ( markup.getFooterHtml() != null ) {
            Document footerDoc = Jsoup.parse("<body>" + markup.getFooterHtml() + "</body>");
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
     * Get asset base directory for the specified campaign content
     * @param campaignContentZip
     * @param localization
     * @return asset base directory
     */
    protected String getAssetBaseDir(CampaignContentZIP campaignContentZip, Localization localization) {
        return StringUtils.join(new String[]{ localization.localizePath(this.assetBaseUrl), campaignContentZip.getId()}, File.separator);
    }

}
