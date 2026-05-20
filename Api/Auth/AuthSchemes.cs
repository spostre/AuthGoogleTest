namespace Api.Auth;

public static class AuthSchemes
{
    /// <summary>
    /// Cookie temporal solo para completar el callback de Google OAuth antes de emitir el JWT.
    /// </summary>
    public const string External = "External";
}
