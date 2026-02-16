namespace ExciseTaxAudit.API.DTOs
{
    public class TaxTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public bool IsActive { get; set; }
    }

    public class ClientDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EIN { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int TaxTypeId { get; set; }
        public bool IsActive { get; set; }
    }

    public class EngagementDto
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string? ExternalEngagementId { get; set; }
        public string EngagementName { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public DateTime FiscalYearStart { get; set; }
        public DateTime FiscalYearEnd { get; set; }
        public string EngagementType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal EstimatedPotentialRecovery { get; set; }
        public decimal ActualRecovery { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateClientRequest
    {
        public string Name { get; set; } = string.Empty;
        public string EIN { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int TaxTypeId { get; set; }
    }

    public class CreateEngagementRequest
    {
        public int ClientId { get; set; }
        public string? ExternalEngagementId { get; set; }
        public string EngagementName { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public DateTime FiscalYearStart { get; set; }
        public DateTime FiscalYearEnd { get; set; }
        public string EngagementType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
