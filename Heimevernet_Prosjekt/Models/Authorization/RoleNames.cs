namespace Heimevernet_Prosjekt.Models.Authorization
{
    public static class RoleNames
    {
        public const string Admin = "admin";
        public const string Operator = "operator";
        public const string User = "user";
        public const string Partner = "partner";

        // Name shown on the website (the values above are what's stored in the database)
        public static string DisplayName(string role) => role switch
        {
            Admin => "Admin",
            Operator => "Operatør",
            User => "User",
            Partner => "Partner",
            _ => role
        };
    }
}