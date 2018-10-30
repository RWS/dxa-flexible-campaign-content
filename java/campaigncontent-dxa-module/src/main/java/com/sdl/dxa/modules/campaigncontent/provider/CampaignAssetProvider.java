package com.sdl.dxa.modules.campaigncontent.provider;

import com.sdl.dxa.modules.campaigncontent.model.CampaignContentZIP;
import com.sdl.webapp.common.api.content.ContentProvider;
import com.sdl.webapp.common.api.content.ContentProviderException;
import com.sdl.webapp.common.api.content.StaticContentItem;
import com.sdl.webapp.common.api.localization.Localization;
import com.tridion.meta.BinaryMeta;
import com.tridion.meta.BinaryMetaFactory;
import lombok.extern.slf4j.Slf4j;
import org.apache.commons.lang3.StringUtils;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;
import org.springframework.web.context.WebApplicationContext;

import java.io.*;
import java.util.HashMap;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/**
 * Campaign Asset Provider
 */
@Component
@Slf4j
public class CampaignAssetProvider {

    @Autowired
    private ContentProvider contentProvider;

    @Autowired
    private WebApplicationContext webApplicationContext;

    private Map<String, CampaignContentMarkup> cachedMarkup = new HashMap<>();

    // Cache time to keep the ZIP file in staging sites.
    // This to avoid to have the ZIP file unzipped for each request on a XPM enabled staging site.
    //
    @Value("${instantcampaign.staging.cacheTime:0}")
    private int stagingCacheTime;

    static final String STATIC_FILES_DIR = "BinaryData";

    /**
     * Get a specific asset in a campaign
     * @param localization
     * @param campaignId
     * @param assetUrl
     * @return asset input stream. If not found -> NULL is returned.
     * @throws ContentProviderException
     */
    public InputStream getAsset(Localization localization, String campaignId, String assetUrl) throws ContentProviderException {

        try {
            log.debug("Getting campaign asset: " + assetUrl + " for campaign ID: " + campaignId);
            File assetFile = new File(getBaseDir(localization, campaignId) + assetUrl);
            if (!assetFile.exists()) {

                // If asset is not available -> trigger unpack the ZIP file & make the campaign markup available.
                // This is to support LB scenario when one node unpacks the ZIP file while other
                // nodes receives the asset requests.
                //
                StaticContentItem zipItem = getZipItem(campaignId, localization);
                if (zipItem == null) {

                    // Campaign asset was not found
                    //
                    return null;
                }
                else {
                    // Trigger unpack of the ZIP file
                    //
                    getCampaignContentMarkup(campaignId, zipItem, localization);
                }
                if (!assetFile.exists()) {
                    log.warn("Campaign asset '" + assetUrl + "' was not found for campaign ID: " + campaignId);
                    return null;
                }
            }
            return new FileInputStream(new File(getBaseDir(localization, campaignId) + assetUrl));
        }
        catch ( IOException e ) {
            throw new ContentProviderException("Could not get asset: " + assetUrl + " for campaign: " + campaignId + " and localication ID: " + localization.getId(), e);
        }

    }

    /**
     * Get the markup (main, header, footer etc) for campaign.
     * @param campaignContentZip
     * @param localization
     * @return markup
     * @throws ContentProviderException
     */
    public CampaignContentMarkup getCampaignContentMarkup(CampaignContentZIP campaignContentZip, Localization localization) throws ContentProviderException {
        StaticContentItem zipItem = this.getZipItem(campaignContentZip, localization);
        return this.getCampaignContentMarkup(campaignContentZip.getId(), zipItem, localization);
    }

    public CampaignContentMarkup getCampaignContentMarkup(String campaignId, StaticContentItem zipItem, Localization localization) throws ContentProviderException {
        String cacheKey = getMarkupCacheKey(campaignId, localization);
        CampaignContentMarkup markup = this.cachedMarkup.get(cacheKey);
        File baseDir = this.getBaseDir(localization, campaignId);

        if( markup == null ||
                !baseDir.exists() ||
                !this.directoryHasFiles(baseDir) ||
                !localization.isStaging() && zipItem.getLastModified() > markup.getLastModified() ||
                localization.isStaging() && markup.getLastModified()+(stagingCacheTime*1000) < System.currentTimeMillis()) {
            try {
                markup = extractZip(zipItem, baseDir);
                markup.setLastModified(zipItem.getLastModified());
                this.cachedMarkup.put(cacheKey, markup);
            }
            catch ( IOException e ) {
                throw new ContentProviderException("Could not extract free format ZIP file.", e);
            }
        }
        return markup;
    }

    /**
     * Get last modified time on the campaign assets (not the content)
     * @param campaignId
     * @param localization
     * @return
     */
    public long getLastModified(String campaignId, Localization localization) {
        CampaignContentMarkup markup = this.cachedMarkup.get(getMarkupCacheKey(campaignId, localization));
        if ( markup != null ) {
            return markup.getLastModified();
        }
        else {
            return -1;
        }
    }

    private String getMarkupCacheKey(String campaignId, Localization localization) {
        return campaignId + "-" + localization.getId();
    }

    /**
     * Get physical base directory for the campaign content
     * @param localization
     * @param campaignId
     * @return base directory
     */
    protected File getBaseDir(Localization localization, String campaignId) {
        return new File(StringUtils.join(new String[]{
                webApplicationContext.getServletContext().getRealPath("/"), STATIC_FILES_DIR, localization.getId(), "campaign-content", campaignId
        }, File.separator));
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

    protected StaticContentItem getZipItem(String itemId, Localization localization) throws ContentProviderException {
        BinaryMetaFactory binaryMetaFactory = new BinaryMetaFactory();
        BinaryMeta binaryMeta = binaryMetaFactory.getMeta("tcm:" + localization.getId() + "-" + itemId);
        if (binaryMeta != null) {
            return contentProvider.getStaticContent(binaryMeta.getURLPath(), localization.getId(), localization.getPath());
        }
        return null;
    }

    /**
     * Extract ZIP
     * @param zipItem
     * @param directory
     * @return HTML fragments
     * @throws IOException
     */
    protected CampaignContentMarkup extractZip(StaticContentItem zipItem, File directory) throws IOException {

        log.debug("Extracting campaign ZIP in directory: " + directory);
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

        CampaignContentMarkup campaignContentMarkup = new CampaignContentMarkup();
        if ( htmlMarkup.size() > 0 ) {
            campaignContentMarkup.setMainHtml(new String(htmlMarkup.toByteArray()));
        }
        if ( headerMarkup.size() > 0 ) {
            campaignContentMarkup.setHeaderHtml(new String(headerMarkup.toByteArray()));
        }
        if ( footerMarkup.size() > 0 ) {
            campaignContentMarkup.setFooterHtml(new String(footerMarkup.toByteArray()));
        }
        return campaignContentMarkup;
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

}
