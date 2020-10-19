Install CMS module
=========================

There is a CMS package that needs be installed in SDL Tridion Sites 9.0/9.1/9.5.

Instructions
-------------

Either SDL Web Content Porter 9.0/9.1/9.5 can be used to import the above packages, or the provided PowerShell script can be used.

#### Instructions for using the PowerShell script:

Before running the import script the needed DLLs needs to be copied. See [Import/Export DLLs](./ImportExport/sites9/README.md) for further information.

Import the CMS package by doing the following

```
.\cms-import.ps1  -cmsUrl [CMS url] -moduleZip CampaignContent-Module-v1.3.0.zip
```

If you already have older version of Instant Campaign installed, you should be able to run this script to update your setup (if you have not changed any of the schemas and templates under '/Building Blocks/Modules/CampaignContent'). If upgrading from v1.2 there is no need to do an upgrade as there is no changes in the CMS templates between v1.2 and v1.3.
