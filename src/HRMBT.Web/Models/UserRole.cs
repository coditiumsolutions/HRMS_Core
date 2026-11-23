namespace HRMBT.Web.Models;

/// <summary>The roles a login can hold. Role names are what [Authorize(Roles = "...")] checks.</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string HR = "HR";

    public static readonly string[] All = { Admin, HR };
}

/// <summary>One role held by one user (table UserRoles). A user can have several rows, e.g. HR and Admin.</summary>
public class UserRole
{
    public int UserId { get; set; }

    public string Role { get; set; } = string.Empty;
}
