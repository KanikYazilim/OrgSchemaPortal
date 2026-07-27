namespace OrgSchema.UI.Models;

public class EmployeeDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ManagerId { get; set; }
    public List<EmployeeDto> Subordinates { get; set; } = new();
    
    // UI state for expand/collapse (Varsayılan olarak kapalı gelsin ki devasa veri ekrana sığsın)
    public bool IsExpanded { get; set; } = false;
}
