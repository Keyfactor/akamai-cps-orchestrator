<h1 align="center" style="border-bottom: none">
    Akamai Certificate Provisioning System (CPS) Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/akamai-cps-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/akamai-cps-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/akamai-cps-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/akamai-cps-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

TODO Overview is a required section



## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The Akamai Certificate Provisioning System (CPS) Universal Orchestrator extension If you have a support issue, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com.

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the Akamai Certificate Provisioning System (CPS) Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.



## Akamai Certificate Store Type

To use the Akamai Certificate Provisioning System (CPS) Universal Orchestrator extension, you **must** create the Akamai Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.



<details><summary>Click to expand details</summary>




#### Supported Operations

| Operation    | Is Supported                                                                                                           |
|--------------|------------------------------------------------------------------------------------------------------------------------|
| Add          | 🔲 Unchecked        |
| Remove       | 🔲 Unchecked     |
| Discovery    | 🔲 Unchecked  |
| Reenrollment | ✅ Checked |
| Create       | 🔲 Unchecked     |

#### Store Type Creation

##### Using kfutil:
`kfutil` is a custom CLI for the Keyfactor Command API and can be used to created certificate store types.
For more information on [kfutil](https://github.com/Keyfactor/kfutil) check out the [docs](https://github.com/Keyfactor/kfutil?tab=readme-ov-file#quickstart)
   <details><summary>Click to expand Akamai kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # Akamai Certificate Provisioning Service
   kfutil store-types create Akamai
   ```

   ##### Offline creation using integration-manifest file:
   If required, it is possible to create store types from the [integration-manifest.json](./integration-manifest.json) included in this repo.
   You would first download the [integration-manifest.json](./integration-manifest.json) and then run the following command
   in your offline environment.
   ```shell
   kfutil store-types create --from-file integration-manifest.json
   ```
   </details>


#### Manual Creation
Below are instructions on how to create the Akamai store type manually in
the Keyfactor Command Portal
   <details><summary>Click to expand manual Akamai details</summary>

   Create a store type called `Akamai` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | Akamai Certificate Provisioning Service | Display name for the store type (may be customized) |
   | Short Name | Akamai | Short display name for the store type |
   | Capability | Akamai | Store type name orchestrator will register with. Check the box to allow entry of value |
   | Supports Add | 🔲 Unchecked |  Indicates that the Store Type supports Management Add |
   | Supports Remove | 🔲 Unchecked |  Indicates that the Store Type supports Management Remove |
   | Supports Discovery | 🔲 Unchecked |  Indicates that the Store Type supports Discovery |
   | Supports Reenrollment | ✅ Checked |  Indicates that the Store Type supports Reenrollment |
   | Supports Create | 🔲 Unchecked |  Indicates that the Store Type supports store creation |
   | Needs Server | 🔲 Unchecked | Determines if a target server name is required when creating store |
   | Blueprint Allowed | 🔲 Unchecked | Determines if store type may be included in an Orchestrator blueprint |
   | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
   | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
   | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

   The Basic tab should look like this:

   ![Akamai Basic Tab](docsource/images/Akamai-basic-store-type-dialog.png)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Forbidden | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Forbidden | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![Akamai Advanced Tab](docsource/images/Akamai-advanced-store-type-dialog.png)

   > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

   ##### Custom Fields Tab
   Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

   | Name | Display Name | Description | Type | Default Value/Options | Required |
   | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
   | access_token | Access Token | The Akamai access_token for authentication. | Secret |  | ✅ Checked |
   | client_token | Client Token | The Akamai client_token for authentication. | Secret |  | ✅ Checked |
   | client_secret | Client Secret | The Akamai client_secret for authentication. | Secret |  | ✅ Checked |

   The Custom Fields tab should look like this:

   ![Akamai Custom Fields Tab](docsource/images/Akamai-custom-fields-store-type-dialog.png)

   ##### Entry Parameters Tab

   | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
   | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
   | EnrollmentId | Enrollment ID | Enrollment ID of a certificate enrollment in Akamai. This should only be supplied for ODKG when replacing an existing certificate. | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | ContractId | Contract ID | The Contract ID of your account in Akamai. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | Sans | SANs | SANs for the new certificate. If multiple are supplied, they should be split with an ampersand character '&' | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-addressLineOne | Admin - Address Line 1 | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-addressLineTwo | Admin - Address Line 2 | Optional field for Administrator contact. | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | admin-city | Admin - City | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-country | Admin - Country | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-email | Admin - Email | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-firstName | Admin - First Name | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-lastName | Admin - Last Name | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-organizationName | Admin - Organization Name | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-phone | Admin - Phone | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-postalCode | Admin - Postal Code | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-region | Admin - Region | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | admin-title | Admin - Title | Required field for Administrator contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | org-addressLineOne | Org - Address Line 1 | Required field for Organization contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | org-addressLineTwo | Org - Address Line 2 | Optional field for Organization contact. | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | org-city | Org - City | Required field for Organization contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | org-country | Org - Country | Required field for Organization contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | org-organizationName | Org - Organization Name | Required field for Organization contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | org-phone | Org - Phone | Required field for Organization contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | org-postalCode | Org - Postal Code | Required field for Organization contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | org-region | Org - Region | Required field for Organization contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-addressLineOne | Tech - Address Line 1 | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-addressLineTwo | Tech - Address Line 2 | Optional field for Akamai Tech contact. | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | tech-city | Tech - City | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-country | Tech - Country | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-email | Tech - Email | Required field for Akamai Tech contact. Must be an akamai.com email address. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-firstName | Tech - First Name | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-lastName | Tech - Last Name | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-organizationName | Tech - Organization Name | Required field for Akamai Tech contact. | String | Akamai | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-phone | Tech - Phone | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-postalCode | Tech - Postal Code | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-region | Tech - Region | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |
   | tech-title | Tech - Title | Required field for Akamai Tech contact. | String | SET-DEFAULT | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | ✅ Checked |

   The Entry Parameters tab should look like this:

   ![Akamai Entry Parameters Tab](docsource/images/Akamai-entry-parameters-store-type-dialog.png)



   </details>

## Installation

1. **Download the latest Akamai Certificate Provisioning System (CPS) Universal Orchestrator extension from GitHub.**

    Navigate to the [Akamai Certificate Provisioning System (CPS) Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/akamai-cps-orchestrator/releases/latest). Refer to the compatibility matrix below to determine whether the `net6.0` or `net8.0` asset should be downloaded. Then, click the corresponding asset to download the zip archive.

   | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `akamai-cps-orchestrator` .NET version to download |
   | --------- | ----------- | ----------- | ----------- |
   | Older than `11.0.0` | | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` |
   | `11.6` _and_ newer | `net8.0` | | `net8.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net6.0`.

2. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`

3. **Create a new directory for the Akamai Certificate Provisioning System (CPS) Universal Orchestrator extension inside the extensions directory.**

    Create a new directory called `akamai-cps-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `akamai-cps-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).


6. **(optional) PAM Integration**

    The Akamai Certificate Provisioning System (CPS) Universal Orchestrator extension is compatible with all supported Keyfactor PAM extensions to resolve PAM-eligible secrets. PAM extensions running on Universal Orchestrators enable secure retrieval of secrets from a connected PAM provider.

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension and follow the associated instructions to install it on the Universal Orchestrator (remote).


> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores



### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "Akamai Certificate Provisioning Service" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path |  |
   | Orchestrator | Select an approved orchestrator capable of managing `Akamai` certificates. Specifically, one with the `Akamai` capability. |
   | access_token | The Akamai access_token for authentication. |
   | client_token | The Akamai client_token for authentication. |
   | client_secret | The Akamai client_secret for authentication. |

</details>



#### Using kfutil CLI

<details><summary>Click to expand details</summary>

1. **Generate a CSV template for the Akamai certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name Akamai --outpath Akamai.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "Akamai Certificate Provisioning Service" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path |  |
   | Orchestrator | Select an approved orchestrator capable of managing `Akamai` certificates. Specifically, one with the `Akamai` capability. |
   | Properties.access_token | The Akamai access_token for authentication. |
   | Properties.client_token | The Akamai client_token for authentication. |
   | Properties.client_secret | The Akamai client_secret for authentication. |

3. **Import the CSV file to create the certificate stores**

    ```shell
    kfutil stores import csv --store-type-name Akamai --file Akamai.csv
    ```

</details>


#### PAM Provider Eligible Fields
<details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.

   | Attribute | Description |
   | --------- | ----------- |
   | access_token | The Akamai access_token for authentication. |
   | client_token | The Akamai client_token for authentication. |
   | client_secret | The Akamai client_secret for authentication. |

Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.
> Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself.

</details>



> The content in this section can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).




## Use Cases

The Akamai CPS orchestrator extension implements the following capabilities:
1. Inventory - Return all certificates of the type defined in the cert store (Production or Staging)
2. Reenrollment - Process a key generation request and create a new certificate with a Keyfactor CA. Two scenarios are supported:
    1. No Enrollment Id provided - create a new Enrollment and certificate in Akamai
    2. Existing Enrollment Id provided - renew an existing certificate in Akamai and update the Enrollment

## Keyfactor Version Supported

The Akamai CPS orchestrator extension requires a Keyfactor Platform version of 9.10 or greater to support encrypted certificate store parameters for authentication.

## Akamai Platform Configuration

In the Akamai instance, an API Credential needs to be configured and used to provide authentication for the Keyfactor Orchestrator extension to function. To do this, navigate to `Account Admin` -> `Identity & access`. Clicking `Create API client`, select a user whose permissions should be used to access and manage certificates. This user should already have the needed permissions to access CPS. The access of the API Client can be restricted to just the CPS APIs, but the API Client should have `READ-WRITE` access.

With the API Client created, a new credential should be generated by clicking `Create credential`. The contents of the credential should be downloaded or saved temporarily to use for configuring the Keyfactor Certificate Store. Afterwards, it should be deleted as the credential file serves as authentication for accessing APIs, and should be treated as a plaintext password and not saved long-term.

## Akamai Orchestrator Extension Configuration

**1a. Use `kfutil` to create the entire store type definition**

Using `kfutil` to create the store type is the preferred method to create the Akamai store type. It will create all of the needed Custom Fields and Entry parameters (of which there are many).

Creating the store can be done by running the following `kfutil` command:

```
kfutil store-types create -n Akamai
```

If using `kfutil`, skip steps __1b__ and __2__ and go to step __3__ to set the default values of the Entry Parameters.

**1b. Manually Create the New Certificate Store Type for the Akamai orchestrator extension**

In Keyfactor Command create a new Certificate Store Type similar to the one below by clicking Settings (the gear icon in the top right) => Certificate Store Types => Add:

![](images/store-type-basic.png)
![](images/store-type-advanced.png)

Custom fields and entry parameters will be added after the store is created. This is required as there are many entry parameters.

**2. Add Custom Fields and Entry Paramaters**

_Only requried if manually adding the certificate store._
To add the needed Custom Fields and Entry Parameters, [run the script](akamai-cps-orchestrator/jobproperties.sql) on the Keyfactor database to generate all the fields and parameters needed.

**3. Set default values of Entry Parameters**

The Entry Parameters are used during Enrollment creation in Akamai CPS to provide contact information and associate new certificates with the correct Contract in Akamai. After adding the parameters, re-open the Certificate Store Type configuration and set the default values.

The Contract ID should be set to the default contract to be used for new Enrollments. All of the address information should be filled out with default expected values, as they are required fields for **each** enrollment created and should not be entered manually unless they need to be overwritten for a specific Enrollment in Akamai.
The Tech contact information should be your Akamai company contact, and needs to have an Akamai email address and should have Akamai as the organization name.

**4. Create a new Akamai Certificate Store**

After the Certificate Store Type has been configured, a new Akamai Certificate Store can be created. When creating the store, the credentials generated in the Akamai platform for the API Client will be used.

| Certificate Store parameter | Akamai credential value |
|-|-|
| Client Machine | `host` |
| Access Token | `access_token` |
| Client Token | `client_token` |
| Client Secret | `client_secret` |

**5. (Optional) Enroll a new certificate in Akamai**

Adding new certificates to Akamai requires generating a key in Akamai CPS via the Reenrollment process in Keyfactor. To start this process, go to the Certificate Store that the certicate should be added to. Select the certificate store, and click the `Reenrollment` button to bring up the reenrollment dialog.

Change any default values as needed, and enter an Enrollment ID if an existing enrollment needs to be updated instead of creating a new Enrollment. This is different from the Slot ID - the Enrollment ID is found by clicking on an Active certificate in Akamai CPS, and looking at the `ID` value.
The SAN entry needs to be filled out with the DNS value you are using for the certificate's CN. If there are multiple DNS SANs, they should be separted with an ampersand. Example: `www.example01.com&www.example02.com`


**6. (Optional) Configure Renewal of Certificates using a Workflow**

Akamai does not support traditional certificate Renewal or one-click Renewal done in the Keyfactor Command platform. The Renewal process creates Certificates with outside keys which are not allowed to be imported into Akamai CPS. As a result, the Reenrollment Job must be used in order to renew existing certificates that reside on the Akamai system. Reenrollment is required as opposed to the Renewal process as it allows Akamai to generate the keys on their platform, which are used to create a certificate in Keyfactor.

Renewing existing certificates in Akamai means running a Reenrollment Job with the same Enrollment ID that was used for an existing Certificate Enrollment. This can be done manually through the Reenrollment prompt, but an automated process can also be configured using a Keyfactor Workflow.

The Workflow should be configured to target a Keyfactor Collection of certificates that includes the Akamai certificates that need to be renewed. This can be done with a query targeting the `CertStoreFQDN` containing `Akamai` and can be further restricted with the `CertStorePath` being equal to `Production` or `Staging`.

A sample workflow for ODKG / Reenrollment scheduling for renewals can be viewed in the [kf-workflow-samples repo](https://github.com/Keyfactor/kf-workflow-samples). When running the sample workflow, it will assume that all certs passed to the script should schedule a Reenrollment job with their existing parameters in Akamai.


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).