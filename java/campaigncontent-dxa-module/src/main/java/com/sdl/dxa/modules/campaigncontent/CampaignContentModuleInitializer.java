package com.sdl.dxa.modules.campaigncontent;

import com.sdl.dxa.modules.campaigncontent.model.CampaignContentZIP;
import com.sdl.webapp.common.api.mapping.views.AbstractInitializer;
import com.sdl.webapp.common.api.mapping.views.ModuleInfo;
import com.sdl.webapp.common.api.mapping.views.RegisteredViewModel;
import com.sdl.webapp.common.api.mapping.views.RegisteredViewModels;
import com.sdl.webapp.common.api.model.page.DefaultPageModel;
import org.springframework.stereotype.Component;

import javax.annotation.PostConstruct;

/**
 * Campaign Content Module Initializer
 *
 * @author nic
 */
@Component
@ModuleInfo(name = "Campaign Content Module", areaName = "CampaignContent", description = "Flexible Campaign Content Module")
@RegisteredViewModels({
        @RegisteredViewModel(viewName = "CampaignContentZIP", modelClass = CampaignContentZIP.class),
        @RegisteredViewModel(viewName = "CampaignPage", modelClass = DefaultPageModel.class),
})
public class CampaignContentModuleInitializer extends AbstractInitializer {

    private static final String AREA_NAME = "CampaignContent";

    @PostConstruct
    public void initialize() throws Exception {
    }

    @Override
    protected String getAreaName() {
        return AREA_NAME;
    }
}
