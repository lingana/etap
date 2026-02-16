namespace ExciseTaxAudit.API.Models;

/// <summary>
/// Tax rate with IRS regulatory citations and context
/// </summary>
public class TaxRateWithCitations
{
    public decimal FederalRate { get; set; }
    public decimal StateRate { get; set; }
    public decimal TotalRate { get; set; }
    public string FuelType { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<string> Citations { get; set; } = new();
    public List<string> Exemptions { get; set; } = new();
    public string? SafeHarborGuidance { get; set; }
    public string? SourcePublication { get; set; }
}

/// <summary>
/// IRS regulatory context from RAG retrieval
/// </summary>
public class IRSRegulatoryContext
{
    public string FuelType { get; set; } = "";
    public string State { get; set; } = "";
    public TaxRateWithCitations RateInfo { get; set; } = new();
    public List<RegulatoryChunk> RelevantRegulations { get; set; } = new();
    public string SummaryGuidance { get; set; } = "";
    public List<string> ApplicableExemptions { get; set; } = new();
    public string Form8849Schedule { get; set; } = "";
}

/// <summary>
/// Chunk of regulatory text retrieved from vector store
/// </summary>
public class RegulatoryChunk
{
    public string DocumentId { get; set; } = "";
    public string DocumentName { get; set; } = "";
    public string Content { get; set; } = "";
    public float RelevanceScore { get; set; }
    public string Section { get; set; } = "";
    public string Citation { get; set; } = "";
}

/// <summary>
/// IRS document metadata for vector store
/// </summary>
public class IRSDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentType { get; set; } = ""; // Publication, Form, Revenue Procedure
    public string PublicationNumber { get; set; } = ""; // e.g., "510", "8849"
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime PublishedDate { get; set; }
    public string? Url { get; set; }
    public List<string> Tags { get; set; } = new();
    public int TaxYear { get; set; }
}

/// <summary>
/// State tax rate from external API
/// </summary>
public class StateTaxRateResponse
{
    public string State { get; set; } = "";
    public string FuelType { get; set; } = "";
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Authority { get; set; } = ""; // e.g., "California Board of Equalization"
    public string Regulation { get; set; } = ""; // e.g., "CA Revenue & Taxation Code §60050"
    public string? ApiSource { get; set; }
}

/// <summary>
/// IRS federal excise tax rate response
/// </summary>
public class IRSRateResponse
{
    public string FuelType { get; set; } = "";
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Source { get; set; } = ""; // Publication number or API source
    public string? Citation { get; set; }
}
