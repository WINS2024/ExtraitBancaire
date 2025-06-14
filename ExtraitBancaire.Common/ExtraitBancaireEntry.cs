namespace ExtraitBancaire.Common;

public class ExtraitBancaireEntry
{
    public DateTime Date { get; set; }
    public string? Libelle { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
} 