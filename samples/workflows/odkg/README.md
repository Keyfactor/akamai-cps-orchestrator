# Sample Workflow: Automated Akamai ODKG (Re-enrollment)

[`sample-odkg-akamai-workflow.json`](sample-odkg-akamai-workflow.json) is a sample Keyfactor Command workflow that
automatically schedules an On-Device Key Generation (ODKG) / Re-enrollment job for certificates that live in an Akamai
certificate store.

Akamai CPS will not accept a certificate whose private key was generated outside of Akamai, so the normal Command
Renewal and one-click Renewal paths cannot be used to renew an Akamai certificate. The only supported path is a
Re-enrollment (ODKG) job that reuses the existing Akamai Enrollment ID, and Keyfactor Command does not currently support
scheduling ODKG jobs automatically. This workflow closes that gap: it watches a certificate collection and, for each
certificate that enters it, reconstructs the original ODKG parameters and submits the Re-enrollment job through the
Keyfactor Command API.

See [Configure Renewal of Certificates using a Workflow](../../../README.md) in the integration README for background on
why ODKG is required.

## Requirements

- **Keyfactor Command 25.x or later.** The workflow uses the `OAuthRESTRequest` workflow step to call the Command API.
- An **OAuth client** (client ID + secret) registered with your identity provider and mapped to a Command identity with
  permissions to:
  - read certificates (`GET /certificates/{id}`),
  - read certificate store types (`GET /certificatestoretypes/name/{name}`),
  - read certificate stores and their inventory (`GET /certificatestores/{id}`, `GET /certificatestores/{id}/inventory`),
  - schedule re-enrollment (`POST /certificatestores/reenrollment`).
- The **Akamai** certificate store type installed, and at least one Akamai certificate store whose inventory has been
  collected. The workflow reads the previous ODKG parameters out of the store inventory, so an inventory job must have
  run successfully against the store before a renewal is attempted.
- A **certificate collection** to act as the trigger (see [Trigger collection](#trigger-collection)).

## Trigger collection

The workflow is a `CertificateEnteredCollection` workflow: it runs once for each certificate that newly enters the
collection named in `Keys`. The collection can be any collection you like, but the recommended query targets active
Akamai certificates expiring within the next seven days:

```
CertStoreType -eq "Akamai" AND ExpirationDate -ge "%TODAY%" AND ExpirationDate -le "%TODAY+7%" AND CertState -eq "1"
```

Adjust the expiration window to match the lead time your team wants. Widening it means renewals start earlier; the
seven-day window assumes the collection is evaluated at least daily.

## What the workflow does

Steps run in the following order (each step's `Outputs.continue` points at the next):

| Order | Step (`UniqueName`) | Type | What it does |
| ----- | ------------------- | ---- | ------------ |
| 1 | `StartNOOP` | NOOP | Entry point. |
| 2 | `PowerShell3` — *Set Workflow Variables* | PowerShell | Publishes the workflow's configuration into the data bucket: `STORE_TYPE_NAME`, `API_URL`, `API_SUBPATH`, `DEFAULT_CA`, `DEFAULT_TEMPLATE`. This is the one step where most configuration lives. |
| 3 | `OAuthRESTRequest1` — *Get ODKG Store Type* | REST | `GET /certificatestoretypes/name/$(STORE_TYPE_NAME)` — resolves the Akamai store type so the correct store location can be picked in step 5. Result → `getStoreTypeResponse`. |
| 4 | `getSANsAndLocationId` — *Get SANs and Location ID* | REST | `GET /certificates/$(certid)?includeLocations=true` — retrieves the triggering certificate along with every store it is deployed to. Result → `getCertificateResponse`. |
| 5 | `PowerShell1` — *Read Values for ODKG Job* | PowerShell | Extracts the DNS SANs (`SubjectAltNameElements` of type `2`) and joins them with `&`, the delimiter Akamai expects → `foundSans`. Then matches the certificate's locations against the Akamai store type to find which store to re-enroll into → `CertStoreId`. |
| 6 | `OAuthRESTRequest3` — *Get Store Config* | REST | `GET /certificatestores/$(CertStoreId)` — used for the store's `AgentId` (the orchestrator that will run the job). Result → `getStoreResponse`. |
| 7 | `OAuthRESTRequest4` — *Get Store Inventory* | REST | `GET /certificatestores/$(CertStoreId)/inventory` — the previous ODKG entry parameters, keyed by certificate thumbprint. Result → `getStoreInventoryResponse`. |
| 8 | `PowerShell2` — *Build ODKG request body* | PowerShell | Assembles the re-enrollment request (see [below](#how-the-odkg-job-is-built)). |
| 9 | `OAuthRESTRequest5` — *Submit ODKG job scheduling request* | REST | `POST /certificatestores/reenrollment` with `Overwrite: true`. |
| 10 | `EndNOOP` | NOOP | Exit point. |

The workflow also relies on Command's built-in certificate context variables: `$(certid)`, `$(thumbprint)`, `$(dn)`,
`$(CA)`, and `$(Template)`.

### How the ODKG job is built

The key idea is that the *previous* ODKG entry parameters are replayed. `PowerShell2` finds the inventory entry whose
alias equals the triggering certificate's thumbprint and reuses that entry's parameter set wholesale — including
`EnrollmentId`, `Deployment Network`, and all admin/org/tech contact fields — then overwrites only `Sans` with the DNS
SANs read from the certificate in step 5.

Because `EnrollmentId` is carried forward, Akamai updates the existing enrollment rather than creating a new one, and no
Contract ID is required.

The resulting request body is:

| Field | Value |
| ----- | ----- |
| `KeystoreId` | The Akamai store the certificate was found in (`CertStoreId` from step 5). |
| `SubjectName` | The certificate's distinguished name (`$(dn)`). |
| `AgentGuid` | `AgentId` from the store configuration. |
| `Alias` | The existing certificate's thumbprint. |
| `JobProperties` | The replayed entry parameters with refreshed `Sans`. |
| `CertificateAuthority` | `DEFAULT_CA` (see [the CA/template caveat](#ca-and-template-selection)), backslash-escaped for the JSON body. |
| `CertificateTemplate` | `DEFAULT_TEMPLATE`. |
| `Overwrite` | `true`. |

## Values you must fill in

Every placeholder in the file is prefixed with `<!`. Search the JSON for `<!` to find them all — the workflow will not
run until each one is replaced. You can edit them in the JSON before import, or fill them in from the workflow designer
after import.

| Placeholder | Where | What to set it to |
| ----------- | ----- | ----------------- |
| `<!OAuth Client ID>` | `client_id` on all five `OAuthRESTRequest` steps | The OAuth client ID used to call the Command API. |
| `<!OAuth Token URL>` | `TokenEndpoint` on all five `OAuthRESTRequest` steps | Your identity provider's token endpoint. |
| `<!API URL>` | `API_URL` in *Set Workflow Variables* | Base URL of Command, e.g. `https://command.example.com`. No trailing slash — the steps add `/$(API_SUBPATH)/...`. |
| `<!API Subpath>` | `API_SUBPATH` in *Set Workflow Variables* | Almost always `KeyfactorAPI`, but confirm it for your environment. |
| `<!CA Host Name>\<!CA Logical Name>` | `DEFAULT_CA` in *Set Workflow Variables* | Fully qualified CA in `hostname\logical name` form, e.g. `ca01.example.com\Example Issuing CA`. |
| `<!Certificate Template>` | `DEFAULT_TEMPLATE` in *Set Workflow Variables* | Short name of the template / enrollment pattern to issue against. |

`STORE_TYPE_NAME` is pre-set to `Akamai`. Change it only if your Akamai certificate store type uses a different short
name.

> [!IMPORTANT]
> The OAuth **client secret** is not part of the exported JSON — `client_secret` is `null` on every REST step. Enter the
> secret on each of the five steps in the workflow designer after import. Do not add the secret to this JSON file, and do
> not commit a copy of the workflow that contains it.

The template and enrollment pattern you choose must support `RSA` or `ECC` keys; those are the only algorithms the
orchestrator supports.

## Importing the workflow

1. Replace the `<!` placeholders as described above.
2. In Keyfactor Command, go to **Workflow > Definitions**, create or open a workflow definition with Type = `CertificateEnteredCollection` and import the JSON file.
3. Review the imported definition and enter the OAuth client secret on each `OAuthRESTRequest` step.
4. Review *Set Workflow Variables* and confirm the values took effect.
5. Publish the definition.


## CA and template selection

`PowerShell2` contains a check that was intended to fall back to `DEFAULT_CA` / `DEFAULT_TEMPLATE` only when the
certificate's own CA is unusable (for example, when the certificate was issued by a CA that is not defined in Command):

```powershell
if($ca -notcontains '\') # $ca was not provided, did not match expected format
{
    $ca = $DEFAULT_CA
    $template = $DEFAULT_TEMPLATE
}
```

> [!NOTE]
> As written, this branch is always taken, so **`DEFAULT_CA` and `DEFAULT_TEMPLATE` are always used** and the
> certificate's own `$(CA)` and `$(Template)` are ignored. PowerShell's `-notcontains` is a collection-membership
> operator, so against a single string it asks whether `$ca` *equals* `\`, which is never true for a real
> `hostname\logical name` value.

For most deployments that behavior is acceptable and arguably desirable: every Akamai renewal goes to one known-good CA
and template. If you want the original intent — reuse the certificate's CA and template, fall back to the defaults only
when the CA is missing or malformed — change the condition to a substring test:

```powershell
if($ca -notlike '*\*')
```

Verify the fallback against a test certificate either way, since a wrong CA or template will fail the job at enrollment
time rather than at workflow time.

## Keyfactor Command version compatibility

Both PowerShell steps contain commented-out lines like:

```powershell
# COMPATIBILITY NOTE: this should be uncommented for KF Command Versions 12.2 and below, which require accessing the Result object
# $certificateResponse = $certificateResponse.Result
```

Command 11 through 12.2 wrapped API responses in a `Result` element; later versions return the payload directly. Leave
these lines commented for Command 25+, which is the only version range this workflow supports (the `OAuthRESTRequest`
step it depends on is not available on those older releases). They are retained only as a reference for anyone adapting
the logic to an older environment.

## Troubleshooting

| Symptom | Likely cause |
| ------- | ------------ |
| Workflow fails at *Read Values for ODKG Job* with no `CertStoreId` | The certificate is not recorded as being in an Akamai store, or `STORE_TYPE_NAME` does not match your store type's short name. Confirm the certificate's Locations in Command. |
| ODKG job is created but Akamai reports that a Contract ID is required | The thumbprint was not found in the store inventory, so no `EnrollmentId` was replayed and Akamai treated the request as a new enrollment. Run an inventory job against the store and confirm the certificate appears in it. |
| ODKG job runs but the SANs are wrong or empty | The certificate has no DNS-type SANs. The workflow only forwards `SubjectAltNameElements` of type `2` (DNS); other SAN types are dropped. |
| Job scheduled against the wrong orchestrator | `AgentGuid` comes from the store's `AgentId`. Check the store's assigned orchestrator in Command. |
| Every renewal uses the same CA/template regardless of the certificate | Expected with the shipped script — see [CA and template selection](#ca-and-template-selection). |
| Any REST step returns 401/403 | The OAuth client secret is missing on that step, or the mapped Command identity lacks permission for that endpoint. Each of the five steps carries its own credentials. |
