using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class LicenseController(IHttpClientFactory httpClientFactory, IConfiguration config) : ControllerBase
{
    private const string PolarValidateUrl = "https://api.polar.sh/v1/license-keys/validate";

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateLicenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest(new { error = "key is required" });

        var apiKey = config["Polar:ApiKey"];
        var benefitId = config["Polar:BenefitId"];
        var organizationId = config["Polar:OrganizationId"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(503, new { error = "License validation not configured" });

        var payload = new Dictionary<string, string?> { ["key"] = request.Key };
        if (!string.IsNullOrWhiteSpace(benefitId))
            payload["benefit_id"] = benefitId;
        if (!string.IsNullOrWhiteSpace(organizationId))
            payload["organization_id"] = organizationId;

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(PolarValidateUrl, content);
        }
        catch
        {
            return StatusCode(502, new { error = "Could not reach Polar.sh" });
        }

        if (!response.IsSuccessStatusCode)
            return Ok(new { valid = false, benefitId = (string?)null, customerEmail = (string?)null });

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var valid = status == "granted";

        string? returnedBenefitId = null;
        if (root.TryGetProperty("benefit_id", out var benefitIdProp))
            returnedBenefitId = benefitIdProp.GetString();

        string? customerEmail = null;
        if (root.TryGetProperty("customer", out var customer) &&
            customer.TryGetProperty("email", out var emailProp))
        {
            customerEmail = emailProp.GetString();
        }

        return Ok(new { valid, benefitId = valid ? returnedBenefitId : null, customerEmail });
    }
}

public record ValidateLicenseRequest(string Key);
