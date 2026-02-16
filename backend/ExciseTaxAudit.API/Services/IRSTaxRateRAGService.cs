using Azure;
using Azure.AI.OpenAI;
using ExciseTaxAudit.API.Models;
using Microsoft.Extensions.Configuration;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// RAG-enhanced service for IRS tax rate lookups with regulatory context
/// </summary>
public class IRSTaxRateRAGService
{
    private readonly VectorStoreService _vectorStore;
    private readonly IRSDataConnectorService _irsConnector;
    private readonly IAzureOpenAIConfigService _openAIConfig;
    private readonly ILogger<IRSTaxRateRAGService> _logger;
    private readonly bool _isEnabled;

    public IRSTaxRateRAGService(
        VectorStoreService vectorStore,
        IRSDataConnectorService irsConnector,
        IAzureOpenAIConfigService openAIConfig,
        ILogger<IRSTaxRateRAGService> logger)
    {
        _vectorStore = vectorStore;
        _irsConnector = irsConnector;
        _openAIConfig = openAIConfig;
        _logger = logger;
        _isEnabled = true; // Service always enabled, uses fallback modes if Azure unavailable
    }

    /// <summary>
    /// Get tax rate with full IRS regulatory context using RAG
    /// </summary>
    public async Task<IRSRegulatoryContext> GetRegulatoryContextAsync(
        string fuelType,
        string state,
        decimal quantity)
    {
        try
        {
            _logger.LogInformation($"Getting regulatory context for {fuelType} in {state}");

            // Step 1: Get current tax rates from IRS/state connectors
            var rateInfo = await _irsConnector.GetCombinedTaxRateAsync(
                fuelType,
                state,
                DateTime.UtcNow
            );

            // Step 2: Retrieve relevant regulations from vector store
            var searchQuery = $"excise tax {fuelType} {state} regulations exemptions Form 8849";
            var regulations = await _vectorStore.SearchRegulationsAsync(searchQuery, maxResults: 5);

            // Step 3: Determine applicable Form 8849 schedule
            var schedule = DetermineForm8849Schedule(fuelType, state);

            // Step 4: Use LLM to synthesize regulatory guidance (if Azure OpenAI available)
            string summaryGuidance;
            if (_isEnabled && regulations.Any())
            {
                summaryGuidance = await GenerateRegulatoryGuidanceAsync(
                    fuelType,
                    state,
                    rateInfo,
                    regulations,
                    quantity
                );
            }
            else
            {
                summaryGuidance = GenerateFallbackGuidance(fuelType, state, rateInfo);
            }

            // Step 5: Extract applicable exemptions
            var exemptions = ExtractApplicableExemptions(regulations, fuelType);

            return new IRSRegulatoryContext
            {
                FuelType = fuelType,
                State = state,
                RateInfo = rateInfo,
                RelevantRegulations = regulations,
                SummaryGuidance = summaryGuidance,
                ApplicableExemptions = exemptions,
                Form8849Schedule = schedule
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting regulatory context: {ex.Message}");
            return GetFallbackContext(fuelType, state, DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Generate regulatory guidance using Azure OpenAI with RAG context
    /// </summary>
    private async Task<string> GenerateRegulatoryGuidanceAsync(
        string fuelType,
        string state,
        TaxRateWithCitations rateInfo,
        List<RegulatoryChunk> regulations,
        decimal? transactionAmount)
    {
        try
        {
            // Create OpenAI client
            var endpoint = new Uri(_openAIConfig.GetEndpoint());
            var credentials = new AzureKeyCredential(_openAIConfig.GetApiKey());
            var client = new OpenAIClient(endpoint, credentials);

            // Build context from retrieved regulations
            var regulatoryContext = string.Join("\n\n", regulations.Select(r =>
                $"[{r.Citation}]\n{r.Content}"
            ));

            var prompt = $@"You are an IRS excise tax compliance expert. Based on the following regulations, provide concise guidance.

FUEL TYPE: {fuelType}
STATE: {state}
FEDERAL RATE: ${rateInfo.FederalRate}/gallon
STATE RATE: ${rateInfo.StateRate}/gallon
TOTAL RATE: ${rateInfo.TotalRate}/gallon

RELEVANT IRS REGULATIONS:
{regulatoryContext}

TASK: Provide a 2-3 sentence summary of the applicable tax rates and key compliance requirements. Include the specific IRS form and schedule for refund claims.

FORMAT: Start with the rate, then cite the regulation, then mention the refund form.";

            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _openAIConfig.GetDeploymentId(),
                Messages =
                {
                    new ChatRequestSystemMessage("You are an expert IRS tax compliance advisor specializing in excise taxes."),
                    new ChatRequestUserMessage(prompt)
                },
                MaxTokens = 200,
                Temperature = 0.3f // Lower temperature for factual compliance info
            };

            var response = await client.GetChatCompletionsAsync(chatOptions);

            if (response?.Value?.Choices?.Count > 0)
            {
                return response.Value.Choices[0].Message.Content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to generate AI guidance: {ex.Message}");
        }

        return GenerateFallbackGuidance(fuelType, state, rateInfo);
    }

    private string GenerateFallbackGuidance(
        string fuelType,
        string state,
        TaxRateWithCitations rateInfo)
    {
        return $"The current excise tax rate for {fuelType} in {state} is ${rateInfo.TotalRate}/gallon " +
               $"(${rateInfo.FederalRate} federal + ${rateInfo.StateRate} state). " +
               $"Per {rateInfo.Citations[0]}, refund claims should be filed using IRS Form 8849, Schedule {DetermineForm8849Schedule(fuelType, state)}. " +
               $"{rateInfo.SafeHarborGuidance}";
    }

    private string DetermineForm8849Schedule(string fuelType, string state)
    {
        // IRS Form 8849 Schedule determination logic
        if (fuelType.Contains("DIESEL", StringComparison.OrdinalIgnoreCase))
        {
            return "2"; // Schedule 2 - Nontaxable Use of Diesel
        }
        else if (fuelType.Contains("GASOLINE", StringComparison.OrdinalIgnoreCase) ||
                 fuelType.Contains("UNLEADED", StringComparison.OrdinalIgnoreCase))
        {
            return "3"; // Schedule 3 - Certain Uses of Gasoline
        }
        else if (fuelType.Contains("KEROSENE", StringComparison.OrdinalIgnoreCase))
        {
            return "5"; // Schedule 5 - Kerosene Used in Aviation
        }
        
        return "2"; // Default to Schedule 2
    }

    private List<string> ExtractApplicableExemptions(
        List<RegulatoryChunk> regulations,
        string fuelType)
    {
        var exemptions = new HashSet<string>();

        // Extract from regulations if available
        foreach (var reg in regulations)
        {
            if (reg.Content.Contains("off-highway", StringComparison.OrdinalIgnoreCase))
                exemptions.Add("Off-highway business use");
            if (reg.Content.Contains("agricultural", StringComparison.OrdinalIgnoreCase))
                exemptions.Add("Agricultural use");
            if (reg.Content.Contains("export", StringComparison.OrdinalIgnoreCase))
                exemptions.Add("Exported fuel");
        }

        // Add common exemptions if none found
        if (!exemptions.Any())
        {
            exemptions.Add("Off-highway business use");
            exemptions.Add("Agricultural use");
            exemptions.Add("State/local government use");
        }

        return exemptions.ToList();
    }

    private IRSRegulatoryContext GetFallbackContext(
        string fuelType,
        string state,
        DateTime transactionDate)
    {
        return new IRSRegulatoryContext
        {
            FuelType = fuelType,
            State = state,
            RateInfo = new TaxRateWithCitations
            {
                FuelType = fuelType,
                State = state,
                FederalRate = 0.184m,
                StateRate = 0.20m,
                TotalRate = 0.384m,
                EffectiveDate = transactionDate,
                Citations = new List<string> { "IRS Publication 510 (Fallback)" },
                SafeHarborGuidance = "Per Rev. Proc. 2011-42: ±10% variance acceptable"
            },
            RelevantRegulations = new List<RegulatoryChunk>(),
            SummaryGuidance = "Unable to retrieve detailed regulatory context. Using standard rates.",
            ApplicableExemptions = new List<string> { "Off-highway business use", "Agricultural use" },
            Form8849Schedule = DetermineForm8849Schedule(fuelType, state)
        };
    }
}
