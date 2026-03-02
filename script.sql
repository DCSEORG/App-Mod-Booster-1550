-- script.sql
-- Grants the managed identity user access to the Northwind database
-- Run via run-sql-dbrole.py after replacing MANAGED-IDENTITY-NAME with the actual identity name

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'MANAGED-IDENTITY-NAME')
BEGIN
    DROP USER [MANAGED-IDENTITY-NAME];
END

CREATE USER [MANAGED-IDENTITY-NAME] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [MANAGED-IDENTITY-NAME];
ALTER ROLE db_datawriter ADD MEMBER [MANAGED-IDENTITY-NAME];
GRANT EXECUTE TO [MANAGED-IDENTITY-NAME];
