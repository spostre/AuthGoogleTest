using Domain.Entities;

namespace Application.Services;

public interface IUserService
{
    Task<Usuario?> GetByIdAsync(int id);
    Task<Usuario?> GetByEmailAsync(string email);
    Task<Usuario?> GetByGoogleIdAsync(string googleId);
    Task<int?> ResolveUserIdFromSubjectAsync(string subject);
    Task<Usuario> RegisterWithGoogleAsync(string googleId, string nombre, string email);
    Task<Usuario> RegisterWithPasswordAsync(string nombre, string email, string password);
    Task<Usuario?> LoginWithPasswordAsync(string email, string password);
}
