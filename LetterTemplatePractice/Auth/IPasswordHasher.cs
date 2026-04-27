namespace LetterTemplatePractice.Auth
{
    /// <summary>
    /// Abstraction over password hashing so the algorithm can be swapped
    /// without touching business logic (Open/Closed Principle).
    /// </summary>
    public interface IPasswordHasher
    {
        string Hash(string plainTextPassword);
        bool   Verify(string plainTextPassword, string hash);
    }
}
