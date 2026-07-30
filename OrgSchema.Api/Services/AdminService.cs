using Dapper;
using Microsoft.Data.SqlClient;
using OrgSchema.Api.Models;

namespace OrgSchema.Api.Services;

public class AdminService
{
    private readonly string _connectionString;

    public AdminService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("Connection string is missing.");
    }

    public async Task<string> EnsureTablesAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HiddenDepartments')
            BEGIN
                CREATE TABLE HiddenDepartments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    DepartmentName NVARCHAR(250) NOT NULL,
                    CreatedAt DATETIME DEFAULT GETDATE()
                );
            END
        ";
        await conn.ExecuteAsync(sql);
        return "TABLES_READY";
    }

    public async Task<List<UnitHierarchyOverrideDto>> GetUnitOverridesAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = "SELECT * FROM Org_UstBirimBakim ORDER BY OlusturmaTarihi DESC";
        var list = await conn.QueryAsync<UnitHierarchyOverrideDto>(sql);
        return list.ToList();
    }

    public async Task SaveUnitOverrideAsync(UnitHierarchyOverrideDto dto)
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = @"
            MERGE Org_UstBirimBakim AS target
            USING (SELECT @BirimId AS BirimId) AS source
            ON target.BirimId = source.BirimId
            WHEN MATCHED THEN
                UPDATE SET 
                    YeniUstBirimId = @YeniUstBirimId,
                    FirmaAdiOverride = @FirmaAdiOverride,
                    Aktif = @Aktif,
                    Notlar = @Notlar
            WHEN NOT MATCHED THEN
                INSERT (BirimId, YeniUstBirimId, FirmaAdiOverride, Aktif, Notlar, OlusturmaTarihi)
                VALUES (@BirimId, @YeniUstBirimId, @FirmaAdiOverride, @Aktif, @Notlar, GETDATE());
        ";
        await conn.ExecuteAsync(sql, dto);
    }

    public async Task DeleteUnitOverrideAsync(int originalUnitId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM Org_UstBirimBakim WHERE BirimId = @Id", new { Id = originalUnitId });
    }

        public async Task<List<UnitSearchDto>> SearchUnitsAsync(string query)
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = "SELECT TOP 20 BirimId, Ad FROM View_ORG_Agac WHERE Ad LIKE @q OR BirimId LIKE @q";
        var list = await conn.QueryAsync<UnitSearchDto>(sql, new { q = "%" + query + "%" });
        return list.ToList();
    }

    public async Task<List<HiddenDepartmentDto>> GetHiddenDepartmentsAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = "SELECT * FROM HiddenDepartments ORDER BY DepartmentName";
        var list = await conn.QueryAsync<HiddenDepartmentDto>(sql);
        return list.ToList();
    }

    public async Task AddHiddenDepartmentAsync(HiddenDepartmentDto dto)
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = "INSERT INTO HiddenDepartments (DepartmentName, CreatedAt) VALUES (@DepartmentName, GETDATE())";
        await conn.ExecuteAsync(sql, dto);
    }

    public async Task DeleteHiddenDepartmentAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM HiddenDepartments WHERE Id = @Id", new { Id = id });
    }
}



