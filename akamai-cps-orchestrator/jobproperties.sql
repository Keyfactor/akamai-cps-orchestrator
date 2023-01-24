declare @id as int

select @id = [StoreType] from [cms_agents].[CertStoreTypes] where [ShortName] = 'Akamai'

insert into [cms_agents].[CertStoreTypeProperties]([StoreTypeId], [Name], [DisplayName], [Type], [Required])
values
	(@id, 'access_token', 'Access Token', 3, 1),
	(@id, 'client_token', 'Client Token', 3, 1),
	(@id, 'client_secret', 'Client Secret', 3, 1)

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
