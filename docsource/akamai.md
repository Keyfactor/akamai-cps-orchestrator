## Overview

> [!WARNING]
> If creating the Certificate Store Type manually, be aware that you will need to save the store-type configuration without entering the custom fields and entry parameters. This is due to a UI limitation. After saving the store type, you will need to run [this SQL script](akamai-cps-orchestrator/jobproperties.sql) on the Keyfactor database to generate all the fields and parameters needed for Akamai CPS.

> [!IMPORTANT]
> To set the `default` values for the Entry Parameters, you will need to re-open the Certificate Store Type configuration after saving and running [this SQL script](akamai-cps-orchestrator/jobproperties.sql). This is due to a UI limitation.

> [!IMPORTANT]
> The `Contract ID` should be set to the [default contract](#contract-id-defaults) to be used for new Enrollments. 
 
> [!IMPORTANT]
> All address information should be filled out with default expected values, as they are required fields for **each** enrollment created and should not be entered manually unless they need to be overwritten for a specific Enrollment in Akamai.

> [!IMPORTANT]
> The Tech contact information should be your Akamai company contact. It must be an Akamai email address (`<contact>@akamai.com`). The contact's organization name must be set to `Akamai`.

## Extension Mechanics

Adding new certificates to Akamai requires generating a key in Akamai CPS via the Reenrollment process in Keyfactor. 
To start this process, go to the Certificate Store that the certificate should be added to. Select the certificate 
store, and click the `Reenrollment` / `OKDG` button to bring up the reenrollment dialog.

Change any `default` values as needed, and enter an `Enrollment ID` if an existing enrollment needs to be updated instead 
of creating a new Enrollment. This is different from the `Slot ID` - the `Enrollment ID` is found by clicking on an 
Active certificate in Akamai CPS, and looking at the `ID` value. The SAN entry needs to be filled out with the DNS value 
you are using for the certificate's CN. If there are multiple DNS SANs, they should be separated with an ampersand (`&`). 
Example: `www.example01.com&www.example02.com`

> [!IMPORTANT]
>
> This extension only supports `RSA` and `ECC` key algorithms. Please make sure the targeted enrollment pattern or certificate template supports these key algorithms.

### Configure Renewal of Certificates using a Workflow
Akamai does not support traditional certificate Renewal or one-click Renewal done in the Keyfactor Command platform. 
The Renewal process creates Certificates with outside keys which are not allowed to be imported into Akamai CPS. As a 
result, the Reenrollment (ODKG) Job must be used in order to renew existing certificates that reside on the Akamai system. 
Reenrollment is required as opposed to the Renewal process as it allows Akamai to generate the keys on their platform, 
which are used to create a certificate in Keyfactor.

Renewing existing certificates in Akamai means running a Reenrollment Job with the same `Enrollment ID` that was used 
for an existing Certificate Enrollment. This can be done manually through the Reenrollment prompt, but an automated 
process can also be configured using a Keyfactor Workflow. The Workflow should be configured to target a Keyfactor 
Collection of certificates that includes the Akamai certificates that need to be renewed. This can be done with a query 
targeting the `CertStoreFQDN` containing `Akamai` and can be further restricted with the `CertStorePath` being equal to 
`Production` or `Staging`. A sample workflow for ODKG / Reenrollment scheduling for renewals can be viewed in the 
[kf-workflow-samples repo](https://github.com/Keyfactor/kf-workflow-samples). When running the sample workflow, it will 
assume that all certs passed to the script should schedule a Reenrollment job with their existing parameters in Akamai.

### Deployment Network Configuration

When enrolling a certificate, you can now specify the `Deployment Network` of the certificate. The valid values are `Standard TLS` and `Enhanced TLS`. If not specified, `Standard TLS` will be used by default for backwards compatibility.

When performing a Reenrollment of an existing certificate, if the `Deployment Network` specified in the Reenrollment configuration is different from the existing enrollment's network type, the existing enrollment network will be preserved. Akamai does not support changing the network type of an existing enrollment. A warning message will be returned in this case to indicate that the network type could not be updated.

If the deployment network for an enrollment needs to be changed, a new enrollment must be created with the desired network type.

> [!NOTE]
>
> If the certificate store type was created before version 2.0.0 of the Akamai CPS Orchestrator, please either update the certificate store type from the UI or utilize [the job properties SQL script](akamai-cps-orchestrator/jobproperties.sql) to add the `Deployment Network` entry parameter to the store type.

### Trust Chain Building

A new feature introduced in 2.0.0 of the Akamai CPS Orchestrator is the ability to build the trust chain of the enrolled certificate and add this to the Akamai trust chain for the certificate.

Currently, only the leaf certificate is returned from Keyfactor Command after enrolling a CSR, so the orchestrator will attempt to build the trust chain with two separate methods:
- Retrieving the intermediate and root certificates using the AIA information in the leaf certificate
- Searching the trust store of the system running the orchestrator for matching intermediate and root certificates (both must be found in order to build the chain)

If the trust chain cannot be built using either of these methods, the orchestrator will still complete the enrollment, but a warning message will be returned indicating that the trust chain could not be built. Please ensure that the intermediate and root certificates are part of your system's trust store or have publicly available AIA information to allow the orchestrator to build the trust chain successfully.

### Contract ID Defaults

In order to create a new enrollment in Akamai CPS, Akamai requires that a [Contract ID](https://techdocs.akamai.com/cps/reference/get-started) be provided. You can find your Contract ID in the Akamai Control Center dashboard. As of v2.1.0 of the Akamai CPS Orchestrator integration, the Contract ID can be specified on the certificate store and on the re-enrollment (ODKG) entry parameter. The certificate store's **Contract ID** will be the default Contract ID for all re-enrollment jobs for the certificate store. The Contract ID re-enrollment entry parameter (**Contract ID Override**) will allow you to override the certificate store Contract ID value, in case you need to specify a different Contract ID for a specific re-enrollment job.

If an Enrollment ID is not provided on the re-enrollment job, a Contract ID will be required to create a new Akamai enrollment. If a Contract ID is not provided on the certificate store or on the re-enrollment entry parameter, the job will fail with the following message: "A Contract ID is required when creating an Akamai enrollment. Please provide a Contract ID on the Reenrollment job or in the certificate store in Keyfactor Command.".

> [!NOTE]
>
> If you created your certificate store prior to v2.1.0 of the integration, you can still run >= v2.1.0 as it is backwards compatible. Your entry parameter will show as **Contract ID** instead of **Contract ID Override**. We do recommend you try to update your certificate store type definition when possible, and we've provided a [SQL script](scripts/migrations/v2.1.0.sql) to help automate this migration.
