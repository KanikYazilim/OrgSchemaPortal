using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// DbContext
builder.Services.AddDbContext<OrgSchema.Api.Data.OrgSchemaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("https://localhost:7280", "http://localhost:5062") // Real ports from launchSettings
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<OrgSchema.Api.Services.IOrgService, OrgSchema.Api.Services.OrgService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazor");

// Default test endpoint
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Message = "OrgSchema API is running" }));

// Org Chart endpoint
app.MapGet("/api/orgchart", async (OrgSchema.Api.Services.IOrgService orgService) =>
{
    var chart = await orgService.GetProcessedOrganizationChartAsync();
    return Results.Ok(chart);
});

app.Run();
