using Dapper;
using Microsoft.Data.SqlClient;

namespace OrgSchema.Api.Services;

/// <summary>
/// Veritabanı keşif servisi - KisiKart2 ve diğer tablolardan YK üyelerini bulmak için
/// </summary>
public class DiagnosticService
{
    private readonly string _connectionStringKYS;
    private readonly string _connectionStringOrgSchema;

    public DiagnosticService(IConfiguration configuration)
    {
        _connectionStringOrgSchema = configuration.GetConnectionString("DefaultConnection") ?? "";
        // KYS veritabanı için ayrı connection (aynı sunucu, farklı DB)
        _connectionStringKYS = configuration.GetConnectionString("KYSConnection") ?? "";
    }

    /// <summary>
    /// KisiKart2 tablosunun kolon yapısını keşfet
    /// </summary>
    public async Task<object> GetKisiKart2SchemaAsync()
    {
        var connStr = !string.IsNullOrWhiteSpace(_connectionStringKYS) ? _connectionStringKYS : _connectionStringOrgSchema;
        using var conn = new SqlConnection(connStr);
        
        try
        {
            var sql = @"SELECT COLUMN_NAME, DATA_TYPE 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'KisiKart2' 
                        ORDER BY ORDINAL_POSITION";
            var columns = await conn.QueryAsync(sql);
            return new { Success = true, Table = "KisiKart2", Columns = columns };
        }
        catch (Exception ex)
        {
            return new { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// KisiKart2'den potansiyel Yönetim Kurulu üyelerini bul
    /// </summary>
    public async Task<object> FindBoardMembersAsync()
    {
        var connStr = !string.IsNullOrWhiteSpace(_connectionStringKYS) ? _connectionStringKYS : _connectionStringOrgSchema;
        using var conn = new SqlConnection(connStr);
        
        try
        {
            // Yöneticisi olmayan veya "Yönetim Kurulu" / "Huzur Hakkı" / "Genel Müdür" içeren kişiler
            var sql = @"SELECT TOP 50 * FROM KisiKart 
                        WHERE DEPAD LIKE '%Yönetim%' 
                           OR DEPAD LIKE '%Huzur%' 
                           OR POS LIKE '%Genel Müdür%'
                           OR POS LIKE '%Yönetim Kurulu%'
                           OR MANAGERSICILNO IS NULL 
                           OR MANAGERSICILNO = '00000000'
                           OR MANAGERSICILNO = SICILNO";
            var members = await conn.QueryAsync(sql);
            return new { Success = true, Count = members.Count(), Members = members };
        }
        catch (Exception ex)
        {
            // KisiKart2 yoksa KisiKart dene
            try 
            {
                var sql2 = @"SELECT TOP 50 * FROM KisiKart2 
                            WHERE POS LIKE '%Yönetim%' 
                               OR POS LIKE '%Huzur%' 
                               OR POS LIKE '%Genel Müdür%'";
                var members2 = await conn.QueryAsync(sql2);
                return new { Success = true, Source = "KisiKart2", Count = members2.Count(), Members = members2 };
            }
            catch (Exception ex2)
            {
                return new { Success = false, Error1 = ex.Message, Error2 = ex2.Message };
            }
        }
    }

    /// <summary>
    /// HROrganizationTable'dan potansiyel YK üyelerini bul
    /// </summary>
    public async Task<object> FindBoardMembersFromHRAsync()
    {
        using var conn = new SqlConnection(_connectionStringOrgSchema);
        
        try
        {
            var sql = @"SELECT TOP 50 * FROM HROrganizationTable 
                        WHERE DEPARTMENTNAME LIKE '%Yönetim%' 
                           OR DEPARTMENTNAME LIKE '%Huzur%' 
                           OR POSITIONNAME LIKE '%Genel Müdür%'
                           OR POSITIONNAME LIKE '%Yönetim%'
                           OR MANAGERUSERID IS NULL 
                           OR MANAGERUSERID = ''
                           OR MANAGERUSERID = USERID";
            var members = await conn.QueryAsync(sql);
            return new { Success = true, Count = members.Count(), Members = members };
        }
        catch (Exception ex)
        {
            return new { Success = false, Error = ex.Message };
        }
    }
}
