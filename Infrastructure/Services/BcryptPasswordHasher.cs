using Application.Common.Interfaces;

namespace Infrastructure.Services;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string passwordHash, string password) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
