namespace ExciseTaxAudit.API.Models
{
    public enum TaxTypeEnum
    {
        FuelTax = 1,
        HUVT = 2,
        AlcoholTax = 3,
        TobaccoTax = 4,
        OtherExciseTax = 5
    }

    public class TaxType
    {
        public int Id { get; set; }
        public TaxTypeEnum Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Client> Clients { get; set; } = new List<Client>();
    }
}
