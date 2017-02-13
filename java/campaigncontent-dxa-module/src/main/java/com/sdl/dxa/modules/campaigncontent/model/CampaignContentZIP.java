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

    private String processedContent;

    public List<TaggedContent> getTaggedContent() {
        return taggedContent;
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
