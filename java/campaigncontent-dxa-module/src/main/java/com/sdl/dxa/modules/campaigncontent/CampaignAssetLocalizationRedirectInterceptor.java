package com.sdl.dxa.modules.campaigncontent;

import com.sdl.webapp.common.api.WebRequestContext;
import com.sdl.webapp.common.api.localization.Localization;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.handler.HandlerInterceptorAdapter;

import javax.servlet.ServletContext;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

/**
 * Campaign Asset Localization Redirect Interceptor
 *
 * @author nic
 */
@Component
public class CampaignAssetLocalizationRedirectInterceptor extends HandlerInterceptorAdapter {

    @Autowired
    private ServletContext servletContext;

    @Autowired
    private WebRequestContext webRequestContext;

    @Value("${instantcampaign.asset.baseUrl:/assets/campaign}")
    // TODO: Right now this has to be hard coded until DXA supports Spring 3.2+
    private String assetBaseUrl;

    @Override
    public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) throws Exception {

        Localization localization = webRequestContext.getLocalization();
        String requestUri = request.getRequestURI();
        String pathPrefix = localization.getPath();
        if ( requestUri.startsWith(pathPrefix) ) {
            requestUri = requestUri.replaceFirst(pathPrefix, "");
            if ( requestUri.startsWith(assetBaseUrl)  ) {

                // Forward request to asset campaign controller
                //
                servletContext.getRequestDispatcher(requestUri).forward(request, response);
                return false;
            }
        }
        return true;
    }
}
