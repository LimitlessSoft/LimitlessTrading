using LT.Public.Api.Data;
using LT.Public.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
            "https://limitlessworkstation.com",
            "https://www.limitlessworkstation.com",
            "http://localhost:4321",
            "http://localhost:8743"
        )
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddSingleton<WidgetCatalogService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseCors();
app.MapControllers();

app.Run();
