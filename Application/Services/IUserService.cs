using Domain.Entities;
using System.Threading.Tasks;

namespace Application.Services;

public interface IUserService
{
    Task<Usuario?> GetByGoogleIdAsync(string googleId);
    Task<Usuario> RegisterAsync(string googleId, string nombre, string email);
}
