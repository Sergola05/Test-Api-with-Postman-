
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ApiTestingDemo.Models;

namespace ApiTestingDemo.Services;

public static class TokenService
{
    public static (string access, string refresh) IssueTokens(
        User user, string role, string issuer, string audience, string key, int accessMinutes, int refreshDays)
    {
        var handler = new JwtSecurityTokenHandler();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, role)
        });
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            Expires = DateTime.UtcNow.AddMinutes(accessMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = handler.CreateToken(descriptor);
        string access = handler.WriteToken(token);
        string refresh = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        return (access, refresh);
    }
}
