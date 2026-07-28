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

builder.Services.AddScoped<OrgSchema.Api.Services.OrganizationService>();
builder.Services.AddScoped<OrgSchema.Api.Services.DiagnosticService>();
builder.Services.AddScoped<OrgSchema.Api.Services.AdminService>();

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
app.MapGet("/api/orgchart", async (OrgSchema.Api.Services.OrganizationService orgService) =>
{
    var chart = await orgService.BuildAsync();
    return Results.Ok(chart);
});

// Flat Employee List endpoint
app.MapGet("/api/employees/flat", async (OrgSchema.Api.Services.OrganizationService orgService) =>
{
    var employees = await orgService.GetFlatEmployeeListAsync();
    return Results.Ok(employees);
});

// ===== ADMIN ENDPOINTS =====

// Tabloyu oluştur
app.MapPost("/api/admin/init", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var result = await adminService.EnsureTableAsync();
    return Results.Ok(new { Status = result });
});

// Tüm çalışanları listele (override bilgisiyle birlikte)
app.MapGet("/api/admin/employees", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var employees = await adminService.GetAllEmployeesAsync();
    return Results.Ok(employees);
});

// Tüm override'ları getir
app.MapGet("/api/admin/overrides", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var overrides = await adminService.GetOverridesAsync();
    return Results.Ok(overrides);
});

// Override kaydet
app.MapPost("/api/admin/overrides", async (OrgSchema.Api.Services.UserOverrideRow data, OrgSchema.Api.Services.AdminService adminService) =>
{
    await adminService.SaveOverrideAsync(data);
    return Results.Ok(new { Status = "Saved", data.USERID });
});

// Override sil
app.MapDelete("/api/admin/overrides/{userId}", async (string userId, OrgSchema.Api.Services.AdminService adminService) =>
{
    await adminService.DeleteOverrideAsync(userId);
    return Results.Ok(new { Status = "Deleted", userId });
});

// Yönetici seçenekleri (dropdown için)
app.MapGet("/api/admin/manager-options", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var options = await adminService.GetManagerOptionsAsync();
    return Results.Ok(options);
});

// YK üyelerini KisiKart2'den otomatik override et
app.MapPost("/api/admin/auto-sync-yk", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var result = await adminService.AutoSyncYkOverridesAsync();
    return Results.Ok(new { Message = result });
});

// Diagnostic endpoints
app.MapGet("/api/diag/board-members", async (OrgSchema.Api.Services.DiagnosticService diagService) =>
{
    var result = await diagService.FindBoardMembersAsync();
    return Results.Ok(result);
});

app.MapGet("/api/diag/board-members-hr", async (OrgSchema.Api.Services.DiagnosticService diagService) =>
{
    var result = await diagService.FindBoardMembersFromHRAsync();
    return Results.Ok(result);
});

app.MapGet("/api/diag/kisikart2-schema", async (OrgSchema.Api.Services.DiagnosticService diagService) =>
{
    var result = await diagService.GetKisiKart2SchemaAsync();
    return Results.Ok(result);
});

app.Run();
