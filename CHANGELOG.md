1.1.0
- Update Expiration Alert Handler sample to filter Subject Elements for duplicates / invalid fields and set the subject to one that will be accepted and matched in the CSR from Akamai
- Additional error handling around renewing / updating existing Akamai Enrollments
- Http-Interface now handles HTTP calls and provides additional logging

1.0.1
- Allow duplicated subject elements to be entered in a Reenrollment request.
- Duplicate subject elements will not be sent to Akamai but can be added during certificate enrollment by a CA.

1.0.0
- Initial release
- Supports single-stacked certificates
- Enrolls third-party certificates for Akamai on a Keyfactor CA
