package com.sdl.dxa.modules.campaigncontent.provider;

import lombok.Getter;
import lombok.Setter;

/**
 * Holder for the different markup in the campaign (main, header, footer)
 */
@Getter
@Setter
public class CampaignContentMarkup {

    private String headerHtml;
    private String mainHtml;
    private String footerHtml;
    long lastModified;
}
