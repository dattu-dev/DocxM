namespace BusinessObjects;

public static class UserRoles
{
    // Role dùng trong authorization attribute và cookie claims.
    public const string Instructor = "Instructor";
    public const string Student = "Student";

    public static bool IsValid(string? role)
    {
        return role is Instructor or Student;
    }
}
