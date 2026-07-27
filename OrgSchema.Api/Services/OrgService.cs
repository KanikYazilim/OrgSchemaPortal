namespace OrgSchema.Api.Services;
using OrgSchema.Api.Models;
using OrgSchema.Api.Data;
using Microsoft.EntityFrameworkCore;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public class OrgService : IOrgService
{
    private readonly OrgSchemaDbContext _dbContext;
    private readonly string _connectionString;

    public OrgService(OrgSchemaDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<List<EmployeeDto>> GetProcessedOrganizationChartAsync()
    {
        // 1. Dapper ile KisiKart2 Tablosundan Gerçek Veriyi Çek
        var rawData = await GetRealSapDataAsync();

        // 2. Bakım Tablosundaki Kuralları Çek
        List<OrgOverrideRule> rules = new();
        try 
        {
            rules = await _dbContext.Org_OverrideRules.Where(r => r.IsActive && !r.IsDeleted).ToListAsync();
        } 
        catch 
        {
            // Eğer veritabanında tablolar henüz tam oluşmadıysa veya hata varsa boş kural döner
            Console.WriteLine("Bakım tabloları okunamadı veya boş.");
        }

        // 3. Kuralları Uygula ve Tekilleştir (In-Memory Processing)
        var processedData = ApplyRulesAndDeduplicate(rawData, rules);

        // 4. Hiyerarşik Ağaç Yapısına Çevir
        var tree = BuildTree(processedData);

        // 5. İzin Verilmeyen Departmanları ve Kilit Olmayan Personelleri Buda
        PruneTree(tree);

        return tree;
    }

    private void PruneTree(List<EmployeeDto> nodes)
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            
            // Önce alt dalları buda
            if (node.Subordinates.Any())
            {
                PruneTree(node.Subordinates);
            }

            // Alt dalları budandıktan sonra kendisine bağlı kimse kalmadıysa kontrol et:
            if (!node.Subordinates.Any())
            {
                // Şirketin ana taşıyıcıları (CEO, Kurul, GM, Direktör) ASLA silinmez!
                if (GetTitlePower(node.Title) >= 80) continue; 
                // Sistem için açtığımız "Yöneticisi Atanmamış" sanal klasörü ASLA silinmez!
                if (node.Id == "ORPHAN_ROOT") continue;

                // Departman kısıtlamasını kaldırdık! 
                // Çünkü önemli biri farklı/eksik isimli bir departmanda olabilir.
                // Sadece kilit personel değilse (işçi, kurye, operatör vs) siliyoruz.
                if (!IsKeyPersonnel(node.Title))
                {
                    nodes.RemoveAt(i);
                }
            }
        }
    }

    private string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text
            .Replace("İ", "I").Replace("i", "I")
            .Replace("I", "I").Replace("ı", "I")
            .Replace("Ş", "S").Replace("ş", "S")
            .Replace("Ğ", "G").Replace("ğ", "G")
            .Replace("Ü", "U").Replace("ü", "U")
            .Replace("Ö", "O").Replace("ö", "O")
            .Replace("Ç", "C").Replace("ç", "C")
            .ToUpperInvariant();
    }

    private bool IsWhitelistedDepartment(string? dept)
    {
        if (string.IsNullOrWhiteSpace(dept)) return false;
        var d = NormalizeText(dept);
        string[] allowedKeywords = { 
            "DIS TICARET", "HUKUK", "INSAN KAYNAKLARI", "MALI ISLER", "PAZARLAMA", "IS GELISTIRME",
            "SATINALMA", "TEKNIK", "ASISTAN", "IDARI ISLER", "LOJISTIK", "SATIS", "URETIM VE BAKIM", 
            "YONETIM", "FABRIKA", "IRC GENEL", "KALITE", "TEDARIK ZINCIRI", "SAP", 
            "TEKNOLOJI", "YAZILIM", "DIJITAL", "YURTDISI", "YURTICI", "SEVKIYAT" 
        };
        return allowedKeywords.Any(k => d.Contains(k));
    }

    private bool IsKeyPersonnel(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = NormalizeText(title);
        string[] allowedKeywords = { 
            "YONETIM", "CEO", "GENEL MUDUR", "CSO", "YARDIMCI", "YRD", 
            "DIREKTOR", "MUDUR", "EKIP LIDERI", "DENETIM", "KURUL", "BASKAN"
        };
        return allowedKeywords.Any(k => t.Contains(k));
    }

    private int GetTitlePower(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return 0;
        var t = title.ToLower();
        
        if (t.Contains("yönetim kurulu") || t.Contains("ceo")) return 100;
        if (t.Contains("genel müdür")) return 90;
        if (t.Contains("direktör") || t.Contains("koordinatör") || t.Contains("başkan")) return 80;
        if (t.Contains("müdür")) return 70;
        if (t.Contains("şef")) return 60;
        if (t.Contains("yönetici")) return 50;
        if (t.Contains("uzman")) return 40;
        if (t.Contains("sorumlu")) return 30;
        if (t.Contains("yardımcı")) return 20;
        if (t.Contains("temsilci")) return 10;
        
        return 0; // Standart personel
    }



    private async Task<List<EmployeeDto>> GetRealSapDataAsync()
    {
        var sql = @"
            SELECT 
                LTRIM(RTRIM(CAST(SICILNO AS NVARCHAR(100)))) AS Id,
                LTRIM(RTRIM(ENAME)) AS FullName,
                LTRIM(RTRIM(KMAIL)) AS Email,
                LTRIM(RTRIM(POZISYONADI)) AS Title,
                LTRIM(RTRIM(DEPARTMANFIRMAADI)) AS Department,
                LTRIM(RTRIM(BIRIMADI)) AS Unit,
                LTRIM(RTRIM(SICILFIRMAADI)) AS Company,
                LTRIM(RTRIM(CAST(MANAGERSICILNO AS NVARCHAR(100)))) AS ManagerId
            FROM KisiKart2
            WHERE AKTIF = 1
        ";

        using var connection = new SqlConnection(_connectionString);
        var result = await connection.QueryAsync<EmployeeDto>(sql);
        return result.ToList();
    }

    private List<EmployeeDto> ApplyRulesAndDeduplicate(List<EmployeeDto> data, List<OrgOverrideRule> rules)
    {
        var result = new List<EmployeeDto>();

        foreach (var item in data)
        {
            // Kullanıcı (Employee) gizleme kontrolü
            var empRule = rules.FirstOrDefault(r => r.TargetType == "Employee" && r.TargetId == item.Id);
            if (empRule?.ActionType == ActionType.Hide) continue;

            // Departman/Birim kuralları
            var deptRule = rules.FirstOrDefault(r => r.TargetType == "Department" && r.TargetId == item.Department);
            if (deptRule != null)
            {
                if (deptRule.ActionType == ActionType.Hide) continue; 
                if (deptRule.ActionType == ActionType.Rename && !string.IsNullOrEmpty(deptRule.NewName))
                {
                    item.Department = deptRule.NewName;
                }
            }
            
            result.Add(item);
        }

        // Akıllı Çalışan Tekilleştirme (Deduplication) - "Unvan Gücü" Algoritması
        // Eğer bir kişinin birden fazla görevi varsa, unvan gücü en yüksek olanı asil kaydı kabul et!
        var deduplicatedResult = result
            .GroupBy(x => !string.IsNullOrWhiteSpace(x.Email) ? x.Email.Trim().ToLower() : x.Id)
            .Select(g => 
            {
                // Gruptaki kayıtları Unvan Gücüne göre büyükten küçüğe sırala ve en baştakini (en güçlü olanı) al
                return g.OrderByDescending(x => GetTitlePower(x.Title)).First();
            })
            .ToList();

        return deduplicatedResult;
    }

    private List<EmployeeDto> BuildTree(List<EmployeeDto> flatList)
    {
        var lookup = flatList.Where(x => !string.IsNullOrEmpty(x.Id)).ToDictionary(x => x.Id!);
        var rootNodes = new List<EmployeeDto>();
        var orphans = new List<EmployeeDto>();

        foreach (var item in flatList)
        {
            // Eğer kişinin yöneticisi yoksa veya DB'de eşleşmiyorsa, onları önce "Sahipsizler" (orphans) havuzuna alıyoruz
            if (string.IsNullOrEmpty(item.ManagerId) || !lookup.ContainsKey(item.ManagerId))
            {
                orphans.Add(item);
            }
            else
            {
                lookup[item.ManagerId].Subordinates.Add(item);
            }
        }

        if (orphans.Any())
        {
            // İçlerindeki en yetkili kişiyi (Genel Yönetici / CEO) buluyoruz (Unvan gücü en yüksek olan)
            var generalManager = orphans.OrderByDescending(x => GetTitlePower(x.Title)).First();
            
            // En Tepeye SADECE Genel Yöneticiyi koyuyoruz
            rootNodes.Add(generalManager);

            // Geri kalan yöneticisi belli olmayan herkesi, mecburen Genel Yöneticiye bağlıyoruz
            foreach (var orphan in orphans.Where(x => x.Id != generalManager.Id))
            {
                generalManager.Subordinates.Add(orphan);
            }
        }

        return rootNodes;
    }
}
