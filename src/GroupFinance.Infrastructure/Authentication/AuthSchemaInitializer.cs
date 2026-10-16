using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GroupFinance.Infrastructure.Persistence;

namespace GroupFinance.Infrastructure.Authentication;

public static class AuthSchemaInitializer
{
    public static async Task InitializeAsync(GroupFinanceDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId bigint NULL,
        ModifiedAtUtc datetime2(0) NULL,
        ModifiedByUserId bigint NULL,
        UserName nvarchar(100) NOT NULL,
        FullName nvarchar(200) NOT NULL,
        Email nvarchar(320) NULL,
        PasswordHash nvarchar(1000) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
        MustChangePassword bit NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0,
        FailedLoginCount int NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT 0,
        LockedUntilUtc datetime2(0) NULL,
        LastLoginAtUtc datetime2(0) NULL,
        PasswordChangedAtUtc datetime2(0) NULL
    );
    CREATE UNIQUE INDEX IX_Users_UserName ON dbo.Users(UserName);
    CREATE INDEX IX_Users_Email ON dbo.Users(Email);
END;
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_Roles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId bigint NULL,
        ModifiedAtUtc datetime2(0) NULL,
        ModifiedByUserId bigint NULL,
        Name nvarchar(100) NOT NULL,
        Description nvarchar(500) NOT NULL
    );
    CREATE UNIQUE INDEX IX_Roles_Name ON dbo.Roles(Name);
END;
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
        CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId bigint NULL,
        ModifiedAtUtc datetime2(0) NULL,
        ModifiedByUserId bigint NULL,
        Code nvarchar(100) NOT NULL,
        Name nvarchar(200) NOT NULL,
        Module nvarchar(100) NOT NULL
    );
    CREATE UNIQUE INDEX IX_Permissions_Code ON dbo.Permissions(Code);
END;
IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserRoles PRIMARY KEY,
        CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_UserRoles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId bigint NULL,
        ModifiedAtUtc datetime2(0) NULL,
        ModifiedByUserId bigint NULL,
        UserId bigint NOT NULL,
        RoleId bigint NOT NULL,
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY(RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_UserRoles_UserId_RoleId ON dbo.UserRoles(UserId, RoleId);
END;
IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RolePermissions PRIMARY KEY,
        CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_RolePermissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId bigint NULL,
        ModifiedAtUtc datetime2(0) NULL,
        ModifiedByUserId bigint NULL,
        RoleId bigint NOT NULL,
        PermissionId bigint NOT NULL,
        CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY(RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY(PermissionId) REFERENCES dbo.Permissions(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_RolePermissions_RoleId_PermissionId ON dbo.RolePermissions(RoleId, PermissionId);
END;
""";
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        logger.LogInformation("Authentication schema is ready.");
    }
}
