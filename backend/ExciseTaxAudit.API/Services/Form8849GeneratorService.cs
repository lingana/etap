using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for generating IRS Form 8849 PDFs for refund claims
/// </summary>
public class Form8849GeneratorService
{
    private readonly AuditContext _context;
    private readonly ILogger<Form8849GeneratorService> _logger;
    private readonly string _formsDirectory;

    public Form8849GeneratorService(
        AuditContext context,
        ILogger<Form8849GeneratorService> logger,
        IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _formsDirectory = Path.Combine(env.WebRootPath ?? "wwwroot", "forms");
        
        // Ensure directory exists
        Directory.CreateDirectory(_formsDirectory);
        
        // QuestPDF License (required for production)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Generate IRS Form 8849 PDF for a refund claim
    /// </summary>
    public async Task<string> GenerateForm8849PdfAsync(int claimId)
    {
        var claim = await _context.RefundClaims
            .Include(c => c.Engagement)
                .ThenInclude(e => e!.Client)
            .FirstOrDefaultAsync(c => c.Id == claimId);

        if (claim == null)
            throw new ArgumentException($"Claim {claimId} not found");

        var fileName = $"Form8849_{claim.ClaimNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
        var filePath = Path.Combine(_formsDirectory, fileName);

        // Generate PDF document
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(50);
                
                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeContent(content, claim));
                page.Footer().Element(ComposeFooter);
            });
        })
        .GeneratePdf(filePath);

        _logger.LogInformation($"Generated Form 8849 PDF: {fileName}");

        return filePath;
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            // IRS Form Header
            column.Item().AlignCenter().Text("Form 8849").FontSize(20).Bold();
            column.Item().AlignCenter().Text("Claim for Refund of Excise Taxes").FontSize(14);
            column.Item().AlignCenter().Text("(Rev. January 2024)").FontSize(10);
            column.Item().AlignCenter().Text("Department of the Treasury - Internal Revenue Service").FontSize(9);
            column.Item().PaddingBottom(20);
        });
    }

    private void ComposeContent(IContainer container, RefundClaim claim)
    {
        container.Column(column =>
        {
            // Section 1: Claimant Information
            column.Item().Element(c => ComposeClaimantInfo(c, claim));
            
            // Section 2: Claim Details
            column.Item().PaddingTop(15).Element(c => ComposeClaimDetails(c, claim));
            
            // Section 3: Tax Computation
            column.Item().PaddingTop(15).Element(c => ComposeTaxComputation(c, claim));
            
            // Section 4: Justification
            column.Item().PaddingTop(15).Element(c => ComposeJustification(c, claim));
            
            // Section 5: IRS Citations
            column.Item().PaddingTop(15).Element(c => ComposeCitations(c, claim));
            
            // Section 6: Signature (placeholder)
            column.Item().PaddingTop(30).Element(ComposeSignature);
        });
    }

    private void ComposeClaimantInfo(IContainer container, RefundClaim claim)
    {
        container.Column(column =>
        {
            column.Item().Text("Part I - Claimant Information").FontSize(12).Bold();
            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Name: {claim.NameOfClaimant ?? "N/A"}").FontSize(10);
                    col.Item().PaddingTop(5).Text($"EIN: {claim.EIN ?? "N/A"}").FontSize(10);
                    col.Item().PaddingTop(5).Text($"Address: {claim.ClaimantAddress ?? "N/A"}").FontSize(10);
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Contact: {claim.ContactName ?? "N/A"}").FontSize(10);
                    col.Item().PaddingTop(5).Text($"Phone: {claim.ContactPhone ?? "N/A"}").FontSize(10);
                    col.Item().PaddingTop(5).Text($"Email: {claim.ContactEmail ?? "N/A"}").FontSize(10);
                });
            });
        });
    }

    private void ComposeClaimDetails(IContainer container, RefundClaim claim)
    {
        container.Column(column =>
        {
            column.Item().Text("Part II - Claim Details").FontSize(12).Bold();
            column.Item().PaddingTop(10).Column(col =>
            {
                col.Item().Text($"Claim Number: {claim.ClaimNumber}").FontSize(10);
                col.Item().PaddingTop(5).Text($"Tax Type: {claim.TaxType}").FontSize(10);
                col.Item().PaddingTop(5).Text($"Tax Form: {claim.TaxFormType}").FontSize(10);
                col.Item().PaddingTop(5).Text($"Refund Type: {claim.RefundType}").FontSize(10);
                col.Item().PaddingTop(5).Text($"Tax Period: {claim.TaxPeriodStart:MM/dd/yyyy} - {claim.TaxPeriodEnd:MM/dd/yyyy}").FontSize(10);
                col.Item().PaddingTop(5).Text($"Tax Year: {claim.TaxYear}").FontSize(10);
                col.Item().PaddingTop(5).Text($"Tax Quarter: {claim.TaxQuarter}").FontSize(10);
            });
        });
    }

    private void ComposeTaxComputation(IContainer container, RefundClaim claim)
    {
        container.Column(column =>
        {
            column.Item().Text("Part III - Tax Computation").FontSize(12).Bold();
            column.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("Description").Bold();
                    header.Cell().Element(CellStyle).Text("Amount").Bold();
                });

                // Rows
                table.Cell().Element(CellStyle).Text("Number of Transactions");
                table.Cell().Element(CellStyle).Text(claim.TransactionCount.ToString());

                table.Cell().Element(CellStyle).Text("Total Refund Claimed");
                table.Cell().Element(CellStyle).Text($"${claim.ClaimedAmount:N2}");

                table.Cell().Element(CellStyle).Text("AI Confidence Score");
                table.Cell().Element(CellStyle).Text($"{claim.ConfidenceScore:P1}");
            });
        });
    }

    private void ComposeJustification(IContainer container, RefundClaim claim)
    {
        container.Column(column =>
        {
            column.Item().Text("Part IV - Justification and Explanation").FontSize(12).Bold();
            column.Item().PaddingTop(10).Text(claim.Justification).FontSize(10);
            
            if (!string.IsNullOrEmpty(claim.AIReasoning))
            {
                column.Item().PaddingTop(10).Text("AI Analysis:").FontSize(10).Bold();
                column.Item().PaddingTop(5).Text(claim.AIReasoning).FontSize(10);
            }
        });
    }

    private void ComposeCitations(IContainer container, RefundClaim claim)
    {
        container.Column(column =>
        {
            column.Item().Text("Part V - IRS Citations and References").FontSize(12).Bold();
            
            try
            {
                var citations = System.Text.Json.JsonSerializer.Deserialize<List<string>>(claim.IRSCitations);
                if (citations != null && citations.Any())
                {
                    foreach (var citation in citations)
                    {
                        column.Item().PaddingTop(5).Text($"• {citation}").FontSize(10);
                    }
                }
                else
                {
                    column.Item().PaddingTop(5).Text("No citations provided").FontSize(10);
                }
            }
            catch
            {
                column.Item().PaddingTop(5).Text("Error parsing citations").FontSize(10);
            }
        });
    }

    private void ComposeSignature(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Part VI - Signature").FontSize(12).Bold();
            column.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Signature: _______________________________").FontSize(10);
                    col.Item().PaddingTop(20).Text("Print Name: _____________________________").FontSize(10);
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Date: ______________").FontSize(10);
                    col.Item().PaddingTop(20).Text("Title: ______________").FontSize(10);
                });
            });
            
            column.Item().PaddingTop(15).Text("Under penalties of perjury, I declare that I have examined this claim, and to the best of my knowledge and belief, it is true, correct, and complete.")
                .FontSize(9)
                .Italic();
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Form 8849 (Rev. 1-2024)").FontSize(8);
            text.Span(" | ").FontSize(8);
            text.Span($"Page 1 | Generated: {DateTime.Now:MM/dd/yyyy}").FontSize(8);
        });
    }

    private IContainer CellStyle(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5);
    }
}
