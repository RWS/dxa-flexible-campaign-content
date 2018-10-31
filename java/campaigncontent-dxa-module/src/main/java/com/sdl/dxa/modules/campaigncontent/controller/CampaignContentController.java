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
    // TODO: Right now this has to be hard coded until DXA supports Spring 4.0+
    private String assetBaseUrl;

    @Value("${instantcampaign.localImages.useParameters:false}")
    private Boolean useParametersForLocalImages;

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
        return this.viewNameResolver.resolveView(mvcData, "Entity");
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

        // Remove all nodes that are marked as Preview Only
        //
        if (true || !webRequestContext.isPreview())
        {
            for (val element : htmlDoc.body().select("[data-preview-only=true]"))
            {
                element.remove();
            }
        }

        // Inject content into placeholders in the markup
        //
        if ( campaignContentZip.getTaggedContent() != null ) {

            int index = 1;
            for (val taggedContent : campaignContentZip.getTaggedContent()) {
                for (val element : htmlDoc.body().select("[data-content-name=" + taggedContent.getName() + "]")) {
                    String contentMarkup = taggedContent.getContent() != null ? taggedContent.getContent().toString() : "";
                    if ( webRequestContext.isPreview() ) {
                        contentMarkup =
                                "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedContent[" +
                                index +
                                "]/custom:content[1]\"} -->" +
                                contentMarkup;
                    }
                    element.html(contentMarkup);
                }
                index++;
            }
        }

        // Inject tagged properties
        //
        if ( campaignContentZip.getTaggedProperties() != null ) {
            int index = 1;
            for (val taggedProperty : campaignContentZip.getTaggedProperties() ) {
                Integer propertyIndex = taggedProperty.getIndex();
                String indexSuffix = "";
                if ( propertyIndex != null && propertyIndex > 1 ) {
                    indexSuffix = "-" + propertyIndex;
                }
                for (val element : htmlDoc.body().select("[data-property-name" + indexSuffix + "=" + taggedProperty.getName() + "]")) {

                    String propertyValue = taggedProperty.getValue();
                    boolean containsUrlPlaceholder = propertyValue.contains("%URL%");
                    if ( taggedProperty.getImage() != null ) {
                        propertyValue = propertyValue.replace("%URL%", taggedProperty.getImage().getUrl());
                        if ( element.tagName().equalsIgnoreCase("img") && StringUtils.isNotEmpty(taggedProperty.getImageAltText())) {
                            element.attr("alt", taggedProperty.getImageAltText());
                        }
                        if ("true".equals(element.attr("data-property-sibling-replace"))) {
                            for (val sibling : element.siblingElements()) {
                                String siblingPropertyValue = sibling.attr(taggedProperty.getTarget() != null ? taggedProperty.getTarget() : "");
                                siblingPropertyValue = siblingPropertyValue.replace("%URL%", taggedProperty.getImage().getUrl());
                                sibling.attr(taggedProperty.getTarget(), siblingPropertyValue);
                                if ( StringUtils.isNotEmpty(taggedProperty.getImageAltText()) && sibling.tagName().equalsIgnoreCase("img")) {
                                    sibling.attr("alt", taggedProperty.getImageAltText());
                                }
                            }
                        }
                    }

                    element.attr(taggedProperty.getTarget(), propertyValue != null ? propertyValue : "");

                    // Generate image XPM markup if tagged property can contain/contains an image URL
                    //
                    if ( webRequestContext.isPreview() && containsUrlPlaceholder) {
                        String xpmMarkup =
                                "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedProperties[" +
                                        index +
                                        "]/custom:image[1]\"} -->";
                        element.prepend(xpmMarkup);
                    }
                }
                index++;
            }
        }

        // Inject tagged links
        //
        if ( campaignContentZip.getTaggedLinks() != null ) {
            int index = 1;
            for (val taggedLink : campaignContentZip.getTaggedLinks() ) {
                for (val element : htmlDoc.body().select("[data-link-name" + "=" + taggedLink.getName() + "]")) {
                    String href = taggedLink.getComponentLink();
                    if ( href == null ) {
                        href = taggedLink.getUrl();
                    }
                    element.attr("href", href != null ? href : "#");
                    if ( webRequestContext.isPreview() ) {
                        String fieldName = taggedLink.getComponentLink() != null || StringUtils.isEmpty(taggedLink.getUrl()) ? "componentLink" : "url";
                        String xpmMarkup =
                                "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedLinks[" +
                                        index +
                                        "]/custom:" + fieldName + "[1]\"} -->";
                        element.prepend(xpmMarkup);
                    }
                }
                index++;
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


                    String imageUrl = "";
                    if ( taggedImage.getImage() != null ) {
                        imageUrl = taggedImage.getImage().getUrl();
                    }
                    else if ( taggedImage.getImageUrl() != null ) {
                        imageUrl = taggedImage.getImageUrl();
                    }
                    if (taggedImage.getParameters() != null &&
                            (taggedImage.getImage() == null || (taggedImage.getImage() != null && Boolean.TRUE.equals(useParametersForLocalImages)))) {
                        imageUrl += "?" + taggedImage.getParameters();
                    }
                    element.attr("src", imageUrl); // Right now always assume the data tag is only used in img tags
                    if (StringUtils.isNotEmpty(taggedImage.getAltText())) {
                        element.attr("alt", taggedImage.getAltText());
                    }
                    if (webRequestContext.isPreview()) {
                        String xpmMarkup =
                                "<!-- Start Component Field: {\"XPath\":\"tcm:Metadata/custom:Metadata/custom:taggedImages[" +
                                        index +
                                        "]/custom:image[1]\"} -->";
                        if (taggedImage.getImage() != null) {
                            element.before(xpmMarkup);
                        }
                        else {
                            // Surround the XPM markup in an additional span. This to avoid that the
                            // image with absolute URL will not disappear as soon you click on it.
                            //
                            element.before("<span>" + xpmMarkup + "</span>");
                        }
                    }

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
