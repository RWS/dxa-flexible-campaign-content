Example Campaign
===============================

Content
----------

The example HTML campaign (ExampleCampaign.zip) is based on the DXA white label design which can be found here:
https://github.com/sdl/dxa-html-design

The ZIP contains the following resources:

- index.html - main HTML for the campaign
- header.html - header for the campaign, which contains CSS includes
- footer.html - footer for the campaign, which contains JS includes
- css
  - example.css - example styles for the example campaign
  - jquery-ui.min.css
- scripts
  - example.js - javascript code for the interaction elements in the campaign
  - jquery-ui.min.js
- images - some demo images (free images from https://www.pexels.com)  

Use the example campaign in DXA white label design project
------------------------------------------------------------

Clone the DXA HTML design from https://github.com/sdl/dxa-html-design or download it from your DXA installation in SDL Web.
Follow the setup steps given in the BUILD.md. After that do the following:

1. Create a new directory under src for the example campaign for example 'example-campaign'.
2. Unzip the ExampleCampaign.zip in this directory
3. Create a simple test HTML file that includes your campaign HTML assets. One example is found here:[./whitelabel-test-index.html](DXA White Label Test HTML Index)
4. Start the HTTP test server by doing: grunt serve
5. Your campaign can for example be accessed via the following URL: http://localhost:9000/example-campaign/whitelabel-test-index.html
