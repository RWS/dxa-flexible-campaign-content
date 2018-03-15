package com.sdl.dxa.modules.campaigncontent.model;

import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticEntity;
import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticProperty;
import com.sdl.webapp.common.api.model.entity.MediaItem;
import com.sdl.webapp.common.exceptions.DxaException;
import com.sdl.webapp.common.markup.html.HtmlElement;

import java.util.List;

import static com.sdl.webapp.common.api.mapping.semantic.config.SemanticVocabulary.SDL_CORE;

/**
 * Campaign Content ZIP
 *
 * @author nic
 */
@SemanticEntity(entityName = "CampaignContentZIP",  vocabulary = SDL_CORE, prefix = "s", public_ = false)
public class CampaignContentZIP extends MediaItem {

    @SemanticProperty("s:taggedContent")
    private List<TaggedContent> taggedContent;

    @SemanticProperty("s:taggedImages")
    private List<TaggedImage> taggedImages;

    @SemanticProperty("s:taggedProperties")
    private List<TaggedProperty> taggedProperties;

    @SemanticProperty("s:taggedLinks")
    private List<TaggedLink> taggedLinks;

    private String processedContent;

    public List<TaggedContent> getTaggedContent() {
        return taggedContent;
    }

    public List<TaggedImage> getTaggedImages() {
        return taggedImages;
    }

    public List<TaggedProperty> getTaggedProperties() {
        return taggedProperties;
    }

    public List<TaggedLink> getTaggedLinks() {
        return taggedLinks;
    }

    public String getProcessedContent() {
        return processedContent;
    }

    public void setProcessedContent(String processedContent) {
        this.processedContent = processedContent;
    }

    @Override
    public HtmlElement toHtmlElement(String widthFactor) throws DxaException {
        return null;
    }

    @Override
    public HtmlElement toHtmlElement(String widthFactor, double aspect, String cssClass, int containerSize) throws DxaException {
        return null;
    }

    @Override
    public HtmlElement toHtmlElement(String widthFactor, double aspect, String cssClass, int containerSize, String contextPath) throws DxaException {
        return null;
    }
}
