using LT.Public.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LT.Public.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WidgetsController(WidgetCatalogService catalog) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(catalog.GetAll());
}
