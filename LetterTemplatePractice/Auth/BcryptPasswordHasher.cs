namespace LetterTemplatePractice.Auth
{
    /// <summary>
    /// BCrypt implementation of <see cref="IPasswordHasher"/>.
    /// Work factor 12 is the current OWASP recommendation for BCrypt.
    /// </summary>
    public sealed class BcryptPasswordHasher : IPasswordHasher
    {
        private const int WorkFactor = 12;

        public string Hash(string plainTextPassword)
            => BCrypt.Net.BCrypt.HashPassword(plainTextPassword, WorkFactor);

        public bool Verify(string plainTextPassword, string hash)
            => BCrypt.Net.BCrypt.Verify(plainTextPassword, hash);
    }
}
