namespace OrgSchema.Api.Models;

public enum SuggestionStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3
}

public class OrgMergeSuggestion
{
    public int Id { get; set; }
    
    // Asıl kelime (Örn: "IT Departmanı")
    public string SourceText { get; set; } = string.Empty;
    
    // Benzer bulunan kelime (Örn: "IT Dept.")
    public string SimilarText { get; set; } = string.Empty;
    
    // 0.0 ile 1.0 arası benzerlik oranı
    public double SimilarityScore { get; set; }
    
    public SuggestionStatus Status { get; set; } = SuggestionStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
