declare @id as int

select @id = [StoreType] from [cms_agents].[CertStoreTypes] where [ShortName] = 'Akamai'

-- This schema is compatible with Keyfactor Command 24 and below
insert into [cms_agents].[CertStoreTypeProperties]([StoreTypeId], [Name], [DisplayName], [Type], [Required])
values
	(@id, 'access_token', 'Access Token', 3, 1),
	(@id, 'client_token', 'Client Token', 3, 1),
	(@id, 'client_secret', 'Client Secret', 3, 1)

-- This schema is compatible with Keyfactor Command 25+
insert into [cms_agents].[CertStoreTypeProperties] (StoreTypeId,Name,DisplayName,[Type],DependsOn,DefaultValue,ValidationOptions) VALUES
	 (@id,N'access_token',N'Access Token',3,NULL,NULL,N'{"OnCreation":1}'),
	 (@id,N'client_token',N'Client Token',3,NULL,NULL,N'{"OnCreation":1}'),
	 (@id,N'client_secret',N'Client Secret',3,NULL,NULL,N'{"OnCreation":1}');


-- This schema is compatible with Keyfactor Command 24 and below
insert into [cms_agents].[CertStoreTypeEntryParameters]([StoreTypeId], [Name], [DisplayName], [Type], [RequiredWhen])
values
	(@id, 'EnrollmentId', 'Enrollment ID', 0, 0),
	(@id, 'ContractId', 'Contract ID', 0, 8),
	(@id, 'Sans', 'SANs', 0, 8),
	(@id, 'admin-addressLineOne', 'Admin - Address Line 1', 0, 8),
	(@id, 'admin-addressLineTwo', 'Admin - Address Line 2', 0, 0),
	(@id, 'admin-city', 'Admin - City', 0, 8), 
	(@id, 'admin-country', 'Admin - Country', 0, 8),
	(@id, 'admin-email', 'Admin - Email', 0, 8),
	(@id, 'admin-firstName', 'Admin - First Name', 0, 8),
	(@id, 'admin-lastName', 'Admin - Last Name', 0, 8),
	(@id, 'admin-organizationName', 'Admin - Organization Name', 0, 8),
	(@id, 'admin-phone', 'Admin - Phone', 0, 8),
	(@id, 'admin-postalCode', 'Admin - Postal Code', 0, 8),
	(@id, 'admin-region', 'Admin - Region', 0, 8),
	(@id, 'admin-title', 'Admin - Title', 0, 8),
	(@id, 'org-addressLineOne', 'Org - Address Line 1', 0, 8),
	(@id, 'org-addressLineTwo', 'Org - Address Line 2', 0, 0),
	(@id, 'org-city', 'Org - City', 0, 8), 
	(@id, 'org-country', 'Org - Country', 0, 8),
	(@id, 'org-organizationName', 'Org - Organization Name', 0, 8),
	(@id, 'org-phone', 'Org - Phone', 0, 8),
	(@id, 'org-postalCode', 'Org - Postal Code', 0, 8),
	(@id, 'org-region', 'Org - Region', 0, 8),
	(@id, 'tech-addressLineOne', 'Tech - Address Line 1', 0, 8),
	(@id, 'tech-addressLineTwo', 'Tech - Address Line 2', 0, 0),
	(@id, 'tech-city', 'Tech - City', 0, 8), 
	(@id, 'tech-country', 'Tech - Country', 0, 8),
	(@id, 'tech-email', 'Tech - Email', 0, 8),
	(@id, 'tech-firstName', 'Tech - First Name', 0, 8),
	(@id, 'tech-lastName', 'Tech - Last Name', 0, 8),
	(@id, 'tech-organizationName', 'Tech - Organization Name', 0, 8),
	(@id, 'tech-phone', 'Tech - Phone', 0, 8),
	(@id, 'tech-postalCode', 'Tech - Postal Code', 0, 8),
	(@id, 'tech-region', 'Tech - Region', 0, 8),
	(@id, 'tech-title', 'Tech - Title', 0, 8)

-- This schema is compatible with Keyfactor Command 25+
INSERT INTO cms_agents.CertStoreTypeEntryParameters (StoreTypeId,Name,DisplayName,[Type],DependsOn,DefaultValue,[Options],ValidationOptions) VALUES
  (@id,N'EnrollmentId',N'Enrollment ID',0,NULL,NULL,NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":0}'),
  (@id,N'ContractId',N'Contract ID',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'Sans',N'SANs',0,NULL,NULL,NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-addressLineOne',N'Admin - Address Line 1',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-addressLineTwo',N'Admin - Address Line 2',0,NULL,NULL,NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":0}'),
  (@id,N'admin-city',N'Admin - City',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-country',N'Admin - Country',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-email',N'Admin - Email',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-firstName',N'Admin - First Name',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-lastName',N'Admin - Last Name',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-organizationName',N'Admin - Organization Name',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-phone',N'Admin - Phone',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-postalCode',N'Admin - Postal Code',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-region',N'Admin - Region',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'admin-title',N'Admin - Title',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'org-addressLineOne',N'Org - Address Line 1',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'org-addressLineTwo',N'Org - Address Line 2',0,NULL,NULL,NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":0}'),
  (@id,N'org-city',N'Org - City',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'org-country',N'Org - Country',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'org-organizationName',N'Org - Organization Name',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'org-phone',N'Org - Phone',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'org-postalCode',N'Org - Postal Code',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'org-region',N'Org - Region',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-addressLineOne',N'Tech - Address Line 1',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-addressLineTwo',N'Tech - Address Line 2',0,NULL,NULL,NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":0}'),
  (@id,N'tech-city',N'Tech - City',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-country',N'Tech - Country',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-email',N'Tech - Email',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-firstName',N'Tech - First Name',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-lastName',N'Tech - Last Name',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-organizationName',N'Tech - Organization Name',0,NULL,N'Akamai',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-phone',N'Tech - Phone',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-postalCode',N'Tech - Postal Code',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-region',N'Tech - Region',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),
  (@id,N'tech-title',N'Tech - Title',0,NULL,N'SET-DEFAULT',NULL,N'{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}');


-- Migration to add Deployment Network entry parameter (requires Command 25+)
insert into [cms_agents].[CertStoreTypeEntryParameters]([StoreTypeId], [Name], [DisplayName], [Type], [DefaultValue], [Options], [ValidationOptions])
values
	(@id, 'deployment-network', 'Deployment Network', 2, 'Standard TLS', 'Standard TLS,Enhanced TLS', '{"HasPrivateKey":0,"OnAdd":0,"OnRemove":0,"OnODKG":1}'),