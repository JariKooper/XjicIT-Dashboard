namespace XjicIT.Dashboard.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly string[] All = [Admin, User];
}

public static class AppClaims
{
    public const string SecurityStamp = "xjic:stamp";
}
