using System.Security.Claims;

namespace TrazeaBlock.Services;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(raw, out var id) ? id : 0;
    }

    public static string GetNombre(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Name)?.Value ?? "";

    public static string GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value ?? "";

    public static string GetRol(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Role)?.Value ?? "";
}
