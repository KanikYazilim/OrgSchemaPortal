using System;

namespace OrgSchema.Api.Models;

public class UnitHierarchyOverrideDto
{
    public int BirimId { get; set; }
    public string? OriginalUnitName { get; set; } // Sadece UI'da gosterim icin
    public int YeniUstBirimId { get; set; }
    public string? NewUstBirimName { get; set; } // Sadece UI'da gosterim icin
    public string? FirmaAdiOverride { get; set; }
    public bool Aktif { get; set; } = true;
    public string? Notlar { get; set; }
    public DateTime? OlusturmaTarihi { get; set; }
}

public class HiddenDepartmentDto
{
    public int Id { get; set; }
    public string DepartmentName { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
}

public class UnitSearchDto
{
    public string? BirimId { get; set; }
    public string? Ad { get; set; }
    public string? FirmaAdi { get; set; }
}
