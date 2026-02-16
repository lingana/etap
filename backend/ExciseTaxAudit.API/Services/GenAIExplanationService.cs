using ExciseTaxAudit.API.Models;
using Azure.AI.OpenAI;
using Azure;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for generating human-readable explanations using Azure OpenAI GenAI.
/// Integrates with Azure OpenAI for natural language explanation generation.
/// </summary>
public class GenAIExplanationService
{
    private readonly ILogger<GenAIExplanationService> _logger;
    private readonly IConfiguration _config;
    private readonly IAzureOpenAIConfigService _openAiConfig;
    private readonly OpenAIClient? _openAiClient;
    private const string ModelDeploymentName = "gpt-35-turbo";

    public GenAIExplanationService(
        ILogger<GenAIExplanationService> logger, 
        IConfiguration config,
        IAzureOpenAIConfigService openAiConfig)
    {
        _logger = logger;
        _config = config;
        _openAiConfig = openAiConfig ?? throw new ArgumentNullException(nameof(openAiConfig));

        try
        {
            var endpoint = new Uri(_openAiConfig.GetEndpoint());
            var apiKey = config["AzureOpenAI:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("AzureOpenAI:ApiKey not configured. Using fallback explanations.");
                _openAiClient = null;
            }
            else
            {
                _openAiClient = new OpenAIClient(endpoint, new AzureKeyCredential(apiKey));
                _logger.LogInformation("Azure OpenAI client initialized successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to initialize Azure OpenAI client: {ex.Message}");
            _openAiClient = null;
        }
    }

    /// <summary>
    /// Generate a human-readable explanation for a flagged transaction using Azure OpenAI.
    /// Falls back to template-based explanation if OpenAI is unavailable.
    /// </summary>
    public async Task<string> GenerateExplanationAsync(TransactionRecord transaction, string anomalyReason, decimal expectedTax)
    {
        try
        {
            if (_openAiClient == null)
            {
                _logger.LogInformation("Azure OpenAI not configured. Using fallback explanation.");
                return GenerateExplanationText(transaction, anomalyReason, expectedTax);
            }

            return await GenerateExplanationWithAzureOpenAIAsync(transaction, anomalyReason, expectedTax);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating explanation: {ex.Message}. Using fallback.");
            return GenerateExplanationText(transaction, anomalyReason, expectedTax);
        }
    }

    /// <summary>
    /// Generate a draft refund claim text for a flagged transaction.
    /// </summary>
    public async Task<string> GenerateClaimDraftAsync(TransactionRecord transaction, decimal expectedTax, decimal difference)
    {
        try
        {
            if (_openAiClient == null)
            {
                return GenerateClaimDraftText(transaction, expectedTax, difference);
            }

            return await GenerateClaimDraftWithAzureOpenAIAsync(transaction, expectedTax, difference);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating claim draft: {ex.Message}. Using fallback.");
            return GenerateClaimDraftText(transaction, expectedTax, difference);
        }
    }

    /// <summary>
    /// Call Azure OpenAI to generate explanation with natural language processing.
    /// </summary>
    private async Task<string> GenerateExplanationWithAzureOpenAIAsync(
        TransactionRecord transaction, 
        string anomalyReason, 
        decimal expectedTax)
    {
        var difference = transaction.TotalTaxAmount - expectedTax;
        var claimType = difference < 0 ? "UNDERPAYMENT" : "OVERPAYMENT";

        var prompt = $@"
As a fuel tax audit specialist, analyze this flagged fuel transaction and provide a concise explanation.

Transaction Details:
- Date: {transaction.TransactionDate:yyyy-MM-dd}
- Merchant: {transaction.MerchantName}, {transaction.MerchantState}
- Fuel Type: {transaction.FuelType}
- Quantity: {transaction.Quantity} {transaction.UOM}
- Price Per Unit: ${transaction.PricePerUnit:F4}
- Reported Tax: ${transaction.TotalTaxAmount:F2}
- Expected Tax: ${expectedTax:F2}
- Variance: ${Math.Abs(difference):F2}
- Claim Type: {claimType}

Anomaly Reason: {anomalyReason}

Generate a professional, concise explanation (2-3 sentences) of why this transaction was flagged and what action is recommended.
Focus on the business impact and tax implications.
";

        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = ModelDeploymentName,
            Messages =
            {
                new ChatRequestSystemMessage("You are a fuel tax compliance expert."),
                new ChatRequestUserMessage(prompt)
            },
            Temperature = 0.3f,
            MaxTokens = 200
        };

        var response = await _openAiClient!.GetChatCompletionsAsync(chatCompletionsOptions);
        var completion = response.Value;

        var explanation = completion.Choices.Count > 0
            ? completion.Choices[0].Message.Content
            : string.Empty;

        if (completion.Usage != null)
        {
            _openAiConfig.LogApiUsage(completion.Usage.PromptTokens, completion.Usage.CompletionTokens);
            _logger.LogInformation(
                "Azure OpenAI tokens - Prompt: {PromptTokens}, Completion: {CompletionTokens}",
                completion.Usage.PromptTokens,
                completion.Usage.CompletionTokens);
        }

        _logger.LogInformation("Generated explanation for transaction {TransactionNumber} using Azure OpenAI", transaction.TransactionNumber);
        return explanation;
    }

    /// <summary>
    /// Call Azure OpenAI to generate a professional claim draft.
    /// </summary>
    private async Task<string> GenerateClaimDraftWithAzureOpenAIAsync(
        TransactionRecord transaction,
        decimal expectedTax,
        decimal difference)
    {
        var claimType = difference > 0 ? "OVERPAYMENT" : "UNDERPAYMENT";

        var prompt = $@"
Generate a professional fuel tax refund claim document for this transaction.

Transaction: {transaction.TransactionNumber}
Date: {transaction.TransactionDate:yyyy-MM-dd}
Merchant: {transaction.MerchantName}, {transaction.MerchantCity}, {transaction.MerchantState}
Fuel Type: {transaction.FuelType}
Quantity: {transaction.Quantity} {transaction.UOM}
Price: ${transaction.PricePerUnit:F4}/unit
Reported Tax: ${transaction.TotalTaxAmount:F2}
Expected Tax: ${expectedTax:F2}
Variance: ${Math.Abs(difference):F2}
Claim Type: {claimType}
Asset: {transaction.AssetDescription}
PADD Region: {transaction.PADDRegion}

Create a concise but complete claim document (3-5 paragraphs) suitable for tax authority submission.
Include transaction details, tax calculation, variance analysis, and recommendation.
";

        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = ModelDeploymentName,
            Messages =
            {
                new ChatRequestSystemMessage("You are a fuel tax compliance and refund claim specialist."),
                new ChatRequestUserMessage(prompt)
            },
            Temperature = 0.3f,
            MaxTokens = 400
        };

        var response = await _openAiClient!.GetChatCompletionsAsync(chatCompletionsOptions);
        var completion = response.Value;

        var claimDraft = completion.Choices.Count > 0
            ? completion.Choices[0].Message.Content
            : string.Empty;

        if (completion.Usage != null)
        {
            _openAiConfig.LogApiUsage(completion.Usage.PromptTokens, completion.Usage.CompletionTokens);
        }

        _logger.LogInformation("Generated claim draft for transaction {TransactionNumber} using Azure OpenAI", transaction.TransactionNumber);
        return claimDraft;
    }

    private string GenerateExplanationText(TransactionRecord transaction, string anomalyReason, decimal expectedTax)
    {
        var difference = transaction.TotalTaxAmount - expectedTax;
        var claimType = difference < 0 ? "potential UNDERPAYMENT" : "potential OVERPAYMENT";

        var explanation = $@"
AUDIT FINDINGS FOR TRANSACTION {transaction.TransactionNumber}
========================================================

Transaction Summary:
- Date: {transaction.TransactionDate:yyyy-MM-dd}
- Merchant: {transaction.MerchantName} ({transaction.MerchantCity}, {transaction.MerchantState})
- Fuel Type: {transaction.FuelType}
- Quantity: {transaction.Quantity} {transaction.UOM} @ ${transaction.PricePerUnit}/unit

Tax Analysis:
- Recorded Tax Amount: ${transaction.TotalTaxAmount:F2}
- Expected Tax Amount: ${expectedTax:F2}
- Variance: ${Math.Abs(difference):F2} ({(difference < 0 ? "Under" : "Over")}charged)

Anomaly Detected:
{anomalyReason}

This suggests a {claimType} situation. The transaction requires manual review 
for potential refund claim eligibility. Please verify state-specific tax rules 
and safe harbor applicability before filing a claim.
";
        return explanation;
    }

    private string GenerateClaimDraftText(TransactionRecord transaction, decimal expectedTax, decimal difference)
    {
        var claimType = difference > 0 ? "OVERPAYMENT" : "UNDERPAYMENT";
        var claimText = $@"
FUEL TAX CLAIM - {claimType}
================================
Transaction Date: {transaction.TransactionDate:yyyy-MM-dd}
Merchant: {transaction.MerchantName}, {transaction.MerchantCity}, {transaction.MerchantState}
Fuel Type: {transaction.FuelType}
Quantity: {transaction.Quantity} {transaction.UOM}
Price Per Unit: ${transaction.PricePerUnit:F4}
Net Cost: ${transaction.NetCost:F2}

Tax Information:
- Reported Tax: ${transaction.TotalTaxAmount:F2}
- Expected Tax: ${expectedTax:F2}
- Difference: ${Math.Abs(difference):F2}
- Claim Type: {claimType}

State: {transaction.MerchantState}
PADD Region: {transaction.PADDRegion}
Asset: {transaction.AssetDescription} (#{transaction.AssetNumber})

Notes:
This claim is based on automated anomaly detection and should be reviewed by a fuel tax specialist.
";
        return claimText;
    }
}


