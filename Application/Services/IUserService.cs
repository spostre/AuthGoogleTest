using Domain.Entities;

namespace Application.Services;

public interface IUserService
{
    Task<Usuario?> GetByIdAsync(int id);
    Task<Usuario?> GetByEmailAsync(string email);
    Task<Usuario?> GetByGoogleIdAsync(string googleId);
    Task<int?> ResolveUserIdFromSubjectAsync(string subject);
    Task<Usuario> RegisterWithGoogleAsync(string googleId, string nombre, string email, string? pictureUrl = null);
    Task<Usuario> RegisterWithPasswordAsync(string nombre, string email, string password);
    Task<Usuario?> LoginWithPasswordAsync(string email, string password);
    Task UpdatePasswordAsync(int userId, string newPassword, string? currentPassword = null);
    Task<Usuario> EnsureGoogleIdLinkedAsync(int userId, string googleId);
    Task<Usuario> LinkGoogleAccountAsync(int userId, string googleId, string googleEmail, string? pictureUrl = null);
    Task<Usuario?> ResolveGoogleUserAsync(string googleId, string email);
    Task<Usuario> UpdatePictureUrlAsync(int userId, string? pictureUrl);
    Task<Usuario> ApplyGooglePictureIfEmptyAsync(int userId, string? pictureUrl);
}
