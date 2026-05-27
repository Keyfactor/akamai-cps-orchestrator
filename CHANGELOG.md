# 2.0.1
## Fixes
- Fixes an issue where some network configuration isn't preserved when updating an existing enrollment.
 
# 2.0.0
Features:
- Add the ability to specify the **Deployment Network** of a certificate (Standard TLS vs Enhanced TLS). By default, Standard TLS certificates will be enrolled.
- Add support for building the trust chain of the enrolled certificate and adding this to the Akamai trust chain for the certificate. Please review the integration documentation for more information.

Certificate Store Type Changes:
- A new entry parameter, `Deployment Network` has been added to specify the network type for the enrolled certificate. Valid values are `Standard TLS` and `Enhanced TLS`. For backwards compatibility, if not specified, `Standard TLS` will be used.

> [!IMPORTANT]
> **BREAKING CHANGES**
> 
> .NET 6 will no longer be a supported target framework. The minimum supported Universal Orchestrator version is now 12.3, which supports .NET 8.

# 1.2.1
- Fixes an issue with parsing subject fields on Re-Enrollment, where a subject field contains a comma.

# 1.2.0
- Add support for retrying API requests that fail due to [Akamai's rate limiting](https://techdocs.akamai.com/cps/reference/rate-limiting)

# 1.1.1
- Point to kf-workflow-samples repo for setting up "renewals" for ODKG certificates
- Remove Powershell script sample previously provided for Expiration Alert Handler renewal process

# 1.1.0
- Update Expiration Alert Handler sample to filter Subject Elements for duplicates / invalid fields and set the subject to one that will be accepted and matched in the CSR from Akamai
- Additional error handling around renewing / updating existing Akamai Enrollments
- Http-Interface now handles HTTP calls and provides additional logging

# 1.0.1
- Allow duplicated subject elements to be entered in a Reenrollment request.
- Duplicate subject elements will not be sent to Akamai but can be added during certificate enrollment by a CA.

# 1.0.0
- Initial release
- Supports single-stacked certificates
- Enrolls third-party certificates for Akamai on a Keyfactor CA
