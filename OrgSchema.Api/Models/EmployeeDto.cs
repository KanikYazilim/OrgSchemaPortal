namespace OrgSchema.Api.Models;

public class EmployeeDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty; // Firma (Kime Danışabilirim)
    public string Unit { get; set; } = string.Empty; // Birim (Kime Danışabilirim)
    public string? Email { get; set; } // Email adresi
    
    // Yöneticisinin ID'si (Hiyerarşi için gerekli)
    // En tepe yöneticide (CEO) bu alan null olur.
    public string? ManagerId { get; set; }
    
    // Altındaki çalışanlar (UI ağacı için JSON hiyerarşisinde kullanılacak)
    public List<EmployeeDto> Subordinates { get; set; } = new();

    public bool IsExpanded { get; set; } = false;
}
