using Dapper;
using Microsoft.Data.SqlClient;

namespace OrgSchema.Api.Services;

public class AdminService
{
    private readonly string _connectionString;

    public AdminService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("Connection string is missing.");
    }

    public async Task<string> EnsureTableAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'HierarchyOverrides')
            BEGIN
                CREATE TABLE HierarchyOverrides (
                    USERID NVARCHAR(100) PRIMARY KEY,
                    MANAGERUSERID NVARCHAR(100) NULL,
                    POSITIONNAME NVARCHAR(250) NULL,
                    DEPARTMENTNAME NVARCHAR(250) NULL,
                    IsHidden BIT DEFAULT 0,
                    SortOrder INT DEFAULT 999,
                    UpdatedAt DATETIME DEFAULT GETDATE(),
                    UpdatedBy NVARCHAR(100) NULL
                );
                SELECT 'CREATED';
            END
            ELSE
                SELECT 'EXISTS';
        ";
        var result = await conn.QueryFirstAsync<string>(sql);
        return result;
    }

    public async Task<List<AdminEmployeeView>> GetAllEmployeesAsync()
    {
        using var conn = new SqlConnection(_connectionString);

        var empSql = "SELECT DISTINCT USERID, ENAME, POSITIONNAME, DEPARTMENTNAME, MANAGERUSERID, COMPANYNAME FROM HROrganizationTable ORDER BY ENAME";
        var employees = (await conn.QueryAsync<AdminEmployeeView>(empSql)).ToList();

        List<UserOverrideRow> overrides;
        try
        {
            var overSql = "SELECT * FROM HierarchyOverrides";
            overrides = (await conn.QueryAsync<UserOverrideRow>(overSql)).ToList();
        }
        catch
        {
            overrides = new List<UserOverrideRow>();
        }

        var overDict = overrides.ToDictionary(o => o.USERID, o => o);
        foreach (var emp in employees)
        {
            if (overDict.TryGetValue(emp.USERID, out var ov))
            {
                emp.Override_MANAGERUSERID = ov.MANAGERUSERID;
                emp.Override_POSITIONNAME = ov.POSITIONNAME;
                emp.Override_DEPARTMENTNAME = ov.DEPARTMENTNAME;
                emp.IsHidden = ov.IsHidden;
                emp.SortOrder = ov.SortOrder;
                emp.HasOverride = true;
            }
        }
        return employees;
    }

    public async Task<List<UserOverrideRow>> GetOverridesAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        try
        {
            var sql = "SELECT * FROM HierarchyOverrides ORDER BY SortOrder, USERID";
            return (await conn.QueryAsync<UserOverrideRow>(sql)).ToList();
        }
        catch
        {
            return new List<UserOverrideRow>();
        }
    }

    public async Task SaveOverrideAsync(UserOverrideRow overrideData)
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = @"
            MERGE HierarchyOverrides AS target
            USING (SELECT @USERID AS USERID) AS source
            ON target.USERID = source.USERID
            WHEN MATCHED THEN
                UPDATE SET 
                    MANAGERUSERID = @MANAGERUSERID,
                    POSITIONNAME = @POSITIONNAME,
                    DEPARTMENTNAME = @DEPARTMENTNAME,
                    IsHidden = @IsHidden,
                    SortOrder = @SortOrder,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (USERID, MANAGERUSERID, POSITIONNAME, DEPARTMENTNAME, IsHidden, SortOrder, UpdatedAt)
                VALUES (@USERID, @MANAGERUSERID, @POSITIONNAME, @DEPARTMENTNAME, @IsHidden, @SortOrder, GETDATE());
        ";
        await conn.ExecuteAsync(sql, overrideData);
    }

    public async Task DeleteOverrideAsync(string userId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM HierarchyOverrides WHERE USERID = @USERID", new { USERID = userId });
    }

    public async Task<List<ManagerOption>> GetManagerOptionsAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        var sql = "SELECT DISTINCT USERID, ENAME, POSITIONNAME FROM HROrganizationTable ORDER BY ENAME";
        return (await conn.QueryAsync<ManagerOption>(sql)).ToList();
    }

    public async Task<string> AutoSyncYkOverridesAsync()
    {
        await EnsureTableAsync();
        using var connection = new SqlConnection(_connectionString);
        
        var sqlKisiKart2 = "SELECT * FROM KisiKart2";
        var all = await connection.QueryAsync(sqlKisiKart2);
        
        var groups = all.GroupBy(x => (string)x.ENAME).Where(g => g.Count() > 1).ToList();
        int syncCount = 0;

        foreach (var g in groups)
        {
            var hasHuzur = g.Any(x => (string)x.BIRIMADI == "Huzur Hakkı" || (string)x.POZISYONADI != null && ((string)x.POZISYONADI).Contains("Huzur Hakkı YK"));
            if (hasHuzur)
            {
                var other = g.FirstOrDefault(x => (string)x.BIRIMADI != "Huzur Hakkı" && !string.IsNullOrWhiteSpace((string)x.BIRIMADI));
                if (other != null)
                {
                    string ename = g.Key;
                    var hrUser = await connection.QueryFirstOrDefaultAsync<string>(
                        "SELECT TOP 1 USERID FROM HROrganizationTable WHERE ENAME = @Ename AND USERID IS NOT NULL", 
                        new { Ename = ename });
                        
                    if (!string.IsNullOrEmpty(hrUser))
                    {
                        var overrideRow = new UserOverrideRow
                        {
                            USERID = hrUser,
                            MANAGERUSERID = hrUser == "SKANIK_OZK" ? null : "SKANIK_OZK",
                            POSITIONNAME = (string)other.POZISYONADI,
                            DEPARTMENTNAME = (string)other.BIRIMADI,
                            IsHidden = false,
                            SortOrder = hrUser == "SKANIK_OZK" ? 1 : 10
                        };
                        
                        await SaveOverrideAsync(overrideRow);
                        syncCount++;
                    }
                }
            }
        }
        return $"Auto-synced {syncCount} YK members from KisiKart2.";
    }
}

// ---- View/DTO Modelleri ----

public class AdminEmployeeView
{
    public string USERID { get; set; } = "";
    public string ENAME { get; set; } = "";
    public string POSITIONNAME { get; set; } = "";
    public string DEPARTMENTNAME { get; set; } = "";
    public string MANAGERUSERID { get; set; } = "";
    public string COMPANYNAME { get; set; } = "";

    // Override alanları
    public string? Override_MANAGERUSERID { get; set; }
    public string? Override_POSITIONNAME { get; set; }
    public string? Override_DEPARTMENTNAME { get; set; }
    public bool IsHidden { get; set; }
    public int SortOrder { get; set; } = 999;
    public bool HasOverride { get; set; }
}

public class UserOverrideRow
{
    public string USERID { get; set; } = "";
    public string? MANAGERUSERID { get; set; }
    public string? POSITIONNAME { get; set; }
    public string? DEPARTMENTNAME { get; set; }
    public bool IsHidden { get; set; }
    public int SortOrder { get; set; } = 999;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ManagerOption
{
    public string USERID { get; set; } = "";
    public string ENAME { get; set; } = "";
    public string POSITIONNAME { get; set; } = "";
}
