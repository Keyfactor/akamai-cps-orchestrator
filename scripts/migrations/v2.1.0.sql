/*
Copyright 2026 Keyfactor

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.



Akamai CPS Orchestrator v2.1.0 Migration Script
===============================================

This script automates the Akamai certificate store type updates to reflect changes in the v2.1.0
integration release. This script does the following:
- Adds a new certificate store property for Contract ID
- Updates the existing Contract ID entry parameter with the following changes:
  1. Changes the Display Name from "Contract ID" to "Contract ID Override"
  2. Removes the DefaultValue from the entry parameter
  3. Updates the validation behavior of the entry parameter to convert it from Required to Optional

NOTE: The script requires that the Contract ID entry parameter must exist before it is run. Please ensure you have successfully created
the certificate store completely prior to running this script.

This script can be safely run multiple times. The DryRun parameter controls whether the script changes
will be committed to the database. You can preview the changes made by this script before committing them.

This script has been designed to work for Keyfactor Command 25+. We cannot guarantee this script will work against future versions of 
Keyfactor Command. Use at your own discretion.
*/

----------------------- BEGIN Input variables	---------------------------
-- The ShortName of the Certificate Store in Keyfactor Command. By default, this should have been populated with Akamai. Modify this field
-- if the ShortName was set to another value
DECLARE @StoreTypeShortName NVARCHAR(64) = 'Akamai';

-- If DryRun = 1, then the script changes will NOT be committed to the database. (Useful for checking if the script works before actually making any changes.)
-- If DryRun = 0, then the script changes WILL BE committed to the database. You'll want to use this value if you're ready to run the script against your database.
DECLARE @DryRun BIT = 1;
-- DECLARE @DryRun BIT = 0;
----------------------- END Input variables	---------------------------

DECLARE @StoreTypeId AS INT;
DECLARE @ContractIdStoreTypePropertyId AS INT;
DECLARE @ContractIdEntryParameterId AS INT;

DECLARE @ContractIdEntryParameterDisplayName NVARCHAR(256);
DECLARE @ContractIdEntryParameterDefaultValue NVARCHAR(100);
DECLARE @ContractIdEntryParameterValidationOptions NVARCHAR(MAX);

PRINT 'Looking up certificate store type ' + @StoreTypeShortName + ' in Keyfactor Command database...';

SELECT @StoreTypeId = [StoreType] from [cms_agents].[CertStoreTypes] where [ShortName] = @StoreTypeShortName;

IF @StoreTypeId IS NULL
BEGIN
    PRINT ''
	PRINT '******** ERROR	***********************'
	PRINT 'ERROR: StoreTypeId is NULL. Was the correct ShortName provided? If you have not already, create the Akamai store type before running this script.'
	RETURN;
END

BEGIN TRY

    PRINT 'Certificate store type ID: ' + CONVERT(NVARCHAR, @StoreTypeId)

    SELECT @ContractIdStoreTypePropertyId = [Id] FROM [cms_agents].[CertStoreTypeProperties] WHERE [StoreTypeId] = @StoreTypeId AND [Name] = 'ContractId';
    SELECT @ContractIdEntryParameterId = [Id], @ContractIdEntryParameterDisplayName = [DisplayName], @ContractIdEntryParameterDefaultValue = [DefaultValue], @ContractIdEntryParameterValidationOptions = [ValidationOptions]  FROM [cms_agents].[CertStoreTypeEntryParameters] WHERE [StoreTypeId] = @StoreTypeId AND [Name] = 'ContractId';

    IF @ContractIdEntryParameterId IS NULL
    BEGIN
        -- It is expected that the Contract ID is already a defined entry parameter on the certificate store. If this is not the case, then we do not want to recreate
        -- this parameter in this script. Encourage the end user to create the certificate store properly before running this script.
        PRINT ''
    	PRINT '******** ERROR	***********************'
    	PRINT 'ERROR: Contract ID Entry Parameter is NULL. The entry parameter was expected to already exist on the certificate store. Please create the Akamai store type before running this script (using kfutil, the jobproperties.sql script, or creating it manually).'
    	RETURN;
    END

    -- We are now ready to start modifying SQL data
    BEGIN TRANSACTION

    IF @ContractIdStoreTypePropertyId IS NULL
    BEGIN
        PRINT 'Creating store type property for Contract ID...';

        INSERT INTO [cms_agents].[CertStoreTypeProperties]
        ([StoreTypeId], [Name], [DisplayName], [Type], [DependsOn], [DefaultValue], [ValidationOptions])
        VALUES
        (@StoreTypeId, 'ContractId', 'Contract ID', 0, NULL, NULL, '{"OnCreation":0}');

        PRINT 'Store type property for Contract ID has been successfully created.';
    END

    IF @ContractIdEntryParameterDisplayName = 'Contract ID'
    BEGIN
        PRINT 'Updating Contract ID Display Name to Contract ID Override...';

        UPDATE [cms_agents].[CertStoreTypeEntryParameters]
        SET [DisplayName] = 'Contract ID Override'
        WHERE [Id] = @ContractIdEntryParameterId;

        PRINT 'Updated Contract ID Display Name to Contract ID Override.';
    END

    IF @ContractIdEntryParameterDefaultValue IS NOT NULL
    BEGIN
        PRINT 'Updating Contract ID Entry Parameter to set the Default Value to NULL...';

        UPDATE [cms_agents].[CertStoreTypeEntryParameters]
        SET [DefaultValue] = NULL
        WHERE [Id] = @ContractIdEntryParameterId;

        PRINT 'Updated Contract ID Entry Parameter to be NULL.';
    END

    IF JSON_VALUE(@ContractIdEntryParameterValidationOptions, '$.OnODKG') = '1'
    BEGIN
        PRINT 'Updating Contract ID Entry Parameter ValidationOptions to make OnODKG optional...';
    
        UPDATE [cms_agents].[CertStoreTypeEntryParameters]
        SET [ValidationOptions] = JSON_MODIFY([ValidationOptions], '$.OnODKG', 0)
        WHERE [Id] = @ContractIdEntryParameterId;
    
        PRINT 'Updated Contract ID Entry Parameter ValidationOptions.';
    END
    
    -- CertStoreTypeProperties changes (preview)
    SELECT
    *
    FROM [cms_agents].[CertStoreTypeProperties]
    WHERE [StoreTypeId] = @StoreTypeId
    AND [Name] = 'ContractId';
    
    -- CertStoreTypeEntryParameters (preview)
    SELECT
    *
    FROM [cms_agents].[CertStoreTypeEntryParameters] 
    WHERE [StoreTypeId] = @StoreTypeId
    AND [Name] = 'ContractId';
    


    IF @DryRun = 1
    BEGIN
    	PRINT 'INFO: This is a dry run of the script. Rolling back any changes made...'
    	ROLLBACK TRANSACTION
    	PRINT 'Rollback successful.'
    END
    ELSE
    BEGIN
    	PRINT 'Committing changes to the database...'
    	COMMIT TRANSACTION
    	PRINT 'Changes committed successfully.'
    END


    PRINT ''
    PRINT 'The script has finished successfully.'

END TRY
BEGIN CATCH
    -- If there is an active transaction, roll it back
	IF XACT_STATE() <> 0
    BEGIN
        PRINT 'Active transaction found. Rolling back.'
	    ROLLBACK TRANSACTION;
    END

    PRINT ''
    PRINT '************** AN UNEXPECTED ERORR OCCURRED *****************************'
    PRINT 'Error number: ' + CAST(ERROR_NUMBER() AS NVARCHAR);
    PRINT 'Message: ' + ERROR_MESSAGE();
END CATCH