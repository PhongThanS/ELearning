-- Tài khoản ứng dụng quyền tối thiểu (docs/09-van-hanh.md mục 4). Chạy SAU migration, bằng tài khoản quản trị.
-- Idempotent: chạy lại chỉ cập nhật mật khẩu và quyền.
-- Biến sqlcmd: DB_NAME, APP_LOGIN, APP_PASSWORD
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(APP_LOGIN)')
    CREATE LOGIN [$(APP_LOGIN)] WITH PASSWORD = N'$(APP_PASSWORD)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DB_NAME)];
ELSE
    ALTER LOGIN [$(APP_LOGIN)] WITH PASSWORD = N'$(APP_PASSWORD)';
GO

USE [$(DB_NAME)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(APP_LOGIN)')
    CREATE USER [$(APP_LOGIN)] FOR LOGIN [$(APP_LOGIN)];

ALTER ROLE db_datareader ADD MEMBER [$(APP_LOGIN)];
ALTER ROLE db_datawriter ADD MEMBER [$(APP_LOGIN)];
GRANT EXECUTE TO [$(APP_LOGIN)];
-- NEXT VALUE FOR cần quyền UPDATE trên sequence (db_datawriter chỉ gồm bảng)
GRANT UPDATE ON OBJECT::dbo.QuestionCodeSequence TO [$(APP_LOGIN)];
-- Không cấp quyền DDL: migration dùng tài khoản riêng
GO

-- Backup log mỗi 15 phút (RPO 15 phút) cần recovery model FULL
ALTER DATABASE [$(DB_NAME)] SET RECOVERY FULL;
GO

PRINT N'App login $(APP_LOGIN) sẵn sàng trên $(DB_NAME).';
