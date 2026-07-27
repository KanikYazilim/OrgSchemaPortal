namespace OrgSchema.Api.Models;

public enum ActionType
{
    Hide = 1,
    Rename = 2,
    Merge = 3
}

public class OrgOverrideRule
{
    public int Id { get; set; }
    
    // SAP'tan gelen orijinal ID veya İsim (Örn: "IT Departmanı")
    public string TargetId { get; set; } = string.Empty;
    
    // "Department" veya "Employee"
    public string TargetType { get; set; } = string.Empty;
    
    // Uygulanacak eylem: Hide, Rename, Merge
    public ActionType ActionType { get; set; }
    
    // Eğer Rename ise, kullanıcıya gösterilecek yeni isim
    public string? NewName { get; set; }
    
    // Eğer Merge ise, bağlanacağı hedefin (Ana Departman/Kişi) ID'si
    public string? MergeTargetId { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Geri alma işlemi (Soft Delete) için
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
