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
        policy.WithOrigins("https://localhost:7301", "http://localhost:5162") // Real ports from launchSettings
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<OrgSchema.Api.Services.OrganizationService>();
builder.Services.AddScoped<OrgSchema.Api.Services.DiagnosticService>();
builder.Services.AddScoped<OrgSchema.Api.Services.AdminService>();

var app = builder.Build();
app.UseWebAssemblyDebugging();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazor");

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Default test endpoint
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Message = "OrgSchema API is running" }));

// Org Chart endpoint
app.MapGet("/api/orgchart", async (OrgSchema.Api.Services.OrganizationService orgService) =>
{
    var chart = await orgService.GetProcessedOrganizationChartAsync();
    return Results.Ok(chart);
});

// Flat Employee List endpoint
app.MapGet("/api/employees/flat", async (OrgSchema.Api.Services.OrganizationService orgService) =>
{
    var employees = await orgService.GetFlatEmployeeListAsync();
    return Results.Ok(employees);
});

// ===== ADMIN ENDPOINTS =====

app.MapPost("/api/admin/init", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var result = await adminService.EnsureTablesAsync();
    return Results.Ok(new { Status = result });
});

// -- UnitHierarchyOverrides --

app.MapGet("/api/admin/unit-overrides", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var overrides = await adminService.GetUnitOverridesAsync();
    return Results.Ok(overrides);
});

app.MapPost("/api/admin/unit-overrides", async (OrgSchema.Api.Models.UnitHierarchyOverrideDto data, OrgSchema.Api.Services.AdminService adminService) =>
{
    await adminService.SaveUnitOverrideAsync(data);
    return Results.Ok(new { Status = "Saved", data.BirimId });
});

app.MapDelete("/api/admin/unit-overrides/{unitId}", async (int unitId, OrgSchema.Api.Services.AdminService adminService) =>
{
    await adminService.DeleteUnitOverrideAsync(unitId);
    return Results.Ok(new { Status = "Deleted", unitId });
});

// -- Search Unit (Autocomplete) --
app.MapGet("/api/admin/search-unit", async (string? q, OrgSchema.Api.Services.AdminService adminService) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Results.Ok(new List<OrgSchema.Api.Models.UnitSearchDto>());
    var results = await adminService.SearchUnitsAsync(q);
    return Results.Ok(results);
});

// -- HiddenDepartments --

app.MapGet("/api/admin/hidden-departments", async (OrgSchema.Api.Services.AdminService adminService) =>
{
    var depts = await adminService.GetHiddenDepartmentsAsync();
    return Results.Ok(depts);
});

app.MapPost("/api/admin/hidden-departments", async (OrgSchema.Api.Models.HiddenDepartmentDto data, OrgSchema.Api.Services.AdminService adminService) =>
{
    await adminService.AddHiddenDepartmentAsync(data);
    return Results.Ok(new { Status = "Added", data.DepartmentName });
});

app.MapDelete("/api/admin/hidden-departments/{id}", async (int id, OrgSchema.Api.Services.AdminService adminService) =>
{
    await adminService.DeleteHiddenDepartmentAsync(id);
    return Results.Ok(new { Status = "Deleted", id });
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

app.MapGet("/api/employees/hierarchy-search", async (string? q, OrgSchema.Api.Services.OrganizationService orgService) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(new List<OrgSchema.Api.Models.FinalEmployeeDto>());
    var results = await orgService.SearchEmployeeHierarchyAsync(q);
    return Results.Ok(results);
});
app.MapFallbackToFile("index.html");

app.Run();




