Install CMS module
=========================

There is a CMS package that needs be installed in SDL Web 8/8.5.

Instructions
-------------

Either SDL Web Content Porter 8/8.5 can be used to import the above packages, or the provided PowerShell script can be used.

#### Instructions for using the PowerShell script:

Before running the import script the needed DLLs needs to be copied. See [Import/Export DLLs](./ImportExport/README.md) for further information.

Import the CMS package by doing the following

```
.\cms-import.ps1  -cmsUrl [CMS url] -moduleZip CampaignContent-Module-v1.2.0.zip
```

If you already have Instant Campaign v1.0/v1.1 installed, you should be able to run this script to update your setup (if you have not changed any of the schemas and templates under '/Building Blocks/Modules/CampaignContent').
