## Overview

> :warning:
> If creating the Certificate Store Type manually, be aware that you will need to save the store-type configuration without entering the custom fields and entry parameters. This is due to a UI limitation. After saving the store type, you will need to run [this SQL script](akamai-cps-orchestrator/jobproperties.sql) on the Keyfactor database to generate all the fields and parameters needed for Akamai CPS.

### Notes:
> :note:
> To set the `default` values for the Entry Parameters, you will need to re-open the Certificate Store Type configuration after saving and running [this SQL script](akamai-cps-orchestrator/jobproperties.sql). This is due to a UI limitation.

> :note:
> The `Contract ID` should be set to the default contract to be used for new Enrollments. 
 
> :note:
> All address information should be filled out with default expected values, as they are required fields for **each** enrollment created and should not be entered manually unless they need to be overwritten for a specific Enrollment in Akamai.

> :note: 
> The Tech contact information should be your Akamai company contact. It must be an Akamai email address (`<contact>@akamai.com`). The contact's organization name must be set to `Akamai`.

## Extension Mechanics

Adding new certificates to Akamai requires generating a key in Akamai CPS via the Reenrollment process in Keyfactor. 
To start this process, go to the Certificate Store that the certificate should be added to. Select the certificate 
store, and click the `Reenrollment` button to bring up the reenrollment dialog.

Change any `default` values as needed, and enter an `Enrollment ID` if an existing enrollment needs to be updated instead 
of creating a new Enrollment. This is different from the `Slot ID` - the `Enrollment ID` is found by clicking on an 
Active certificate in Akamai CPS, and looking at the `ID` value. The SAN entry needs to be filled out with the DNS value 
you are using for the certificate's CN. If there are multiple DNS SANs, they should be separated with an ampersand (`&`). 
Example: `www.example01.com&www.example02.com`

### Configure Renewal of Certificates using a Workflow
Akamai does not support traditional certificate Renewal or one-click Renewal done in the Keyfactor Command platform. 
The Renewal process creates Certificates with outside keys which are not allowed to be imported into Akamai CPS. As a 
result, the Reenrollment Job must be used in order to renew existing certificates that reside on the Akamai system. 
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