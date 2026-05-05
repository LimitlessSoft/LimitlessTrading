using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LT.Public.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var adminEmail = config["Admin:Email"] ?? "";
        var adminPassword = config["Admin:Password"] ?? "";
        var jwtSecret = config["Admin:JwtSecret"] ?? "dev-secret-change-in-production";

        if (!string.Equals(req.Email, adminEmail, StringComparison.OrdinalIgnoreCase) ||
            req.Password != adminPassword)
            return Unauthorized(new { error = "Invalid credentials" });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "LimitlessTrading",
            audience: "LimitlessTrading",
            claims: [new Claim(ClaimTypes.Email, req.Email)],
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}
