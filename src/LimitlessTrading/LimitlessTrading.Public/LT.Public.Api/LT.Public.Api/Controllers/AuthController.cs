using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LT.Public.Api.Data;
using LT.Public.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IConfiguration config, AppDbContext db) : ControllerBase
{
    private const string PasswordHashKey = "password_hash";

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100_000, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100_000, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private async Task<bool> ValidatePassword(string password)
    {
        var stored = await db.AdminSettings.FindAsync(PasswordHashKey);
        if (stored is not null)
            return VerifyPassword(password, stored.Value);

        // Fall back to plaintext config value on first use
        return password == (config["Admin:Password"] ?? "");
    }

    private string IssueToken(string email)
    {
        var jwtSecret = config["Admin:JwtSecret"] ?? "dev-secret-change-in-production";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "LimitlessTrading",
            audience: "LimitlessTrading",
            claims: [new Claim(ClaimTypes.Email, email)],
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var adminEmail = config["Admin:Email"] ?? "";
        if (!string.Equals(req.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { error = "Invalid credentials" });

        if (!await ValidatePassword(req.Password))
            return Unauthorized(new { error = "Invalid credentials" });

        return Ok(new { token = IssueToken(req.Email) });
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest(new { error = "New password must be at least 8 characters" });

        if (!await ValidatePassword(req.CurrentPassword))
            return BadRequest(new { error = "Current password is incorrect" });

        var hash = HashPassword(req.NewPassword);
        var setting = await db.AdminSettings.FindAsync(PasswordHashKey);
        if (setting is null)
            db.AdminSettings.Add(new Models.AdminSetting { Key = PasswordHashKey, Value = hash });
        else
            setting.Value = hash;

        await db.SaveChangesAsync();
        return Ok(new { message = "Password updated" });
    }
}
