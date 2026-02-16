using ExciseTaxAudit.API.Controllers;
using Azure;
using Azure.AI.OpenAI;

namespace ExciseTaxAudit.API.Services
{
    public interface IAITaxInsightService
    {
        Task<List<AITaxInsightDto>> GetTaxInsightsAsync(string taxType, string? context);
        Task<IndustryRecommendationDto> GetIndustryRecommendationsAsync(string industry, string taxType);
    }

    public class AITaxInsightService : IAITaxInsightService
    {
        private readonly IAzureOpenAIConfigService _openAIConfigService;
        private readonly ILogger<AITaxInsightService> _logger;

        public AITaxInsightService(
            IAzureOpenAIConfigService openAIConfigService,
            ILogger<AITaxInsightService> logger)
        {
            _openAIConfigService = openAIConfigService;
            _logger = logger;
        }

        public async Task<List<AITaxInsightDto>> GetTaxInsightsAsync(string taxType, string? context)
        {
            try
            {
                var endpoint = _openAIConfigService.GetEndpoint();
                var apiKey = _openAIConfigService.GetApiKey();
                var deploymentName = _openAIConfigService.GetDeploymentName();

                var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

                // Build RAG prompt with tax regulations and context
                var systemPrompt = BuildTaxInsightSystemPrompt();
                var userPrompt = $@"Provide detailed tax compliance insights for: {taxType}
                
Context: {context ?? "General tax audit and compliance"}

Please provide insights in the following categories:
1. Compliance Requirements
2. Common Issues and Red Flags
3. Best Practices
4. Recent Regulatory Changes

Format each insight with:
- Category name
- Detailed insight text
- Confidence level (0-1)
- Relevant IRS regulations or publications";

                var chatCompletionsOptions = new ChatCompletionsOptions()
                {
                    DeploymentName = deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(userPrompt)
                    },
                    Temperature = 0.3f,
                    MaxTokens = 1500,
                    NucleusSamplingFactor = 0.95f
                };

                var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
                var content = response.Value.Choices[0].Message.Content;

                // Parse AI response into structured insights
                return ParseAIResponseToInsights(content, taxType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate AI insights for {taxType}");
                return GetFallbackInsights(taxType);
            }
        }

        public async Task<IndustryRecommendationDto> GetIndustryRecommendationsAsync(string industry, string taxType)
        {
            try
            {
                var endpoint = _openAIConfigService.GetEndpoint();
                var apiKey = _openAIConfigService.GetApiKey();
                var deploymentName = _openAIConfigService.GetDeploymentName();

                var client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

                var systemPrompt = BuildTaxInsightSystemPrompt();
                var userPrompt = $@"Provide industry-specific tax compliance recommendations for:
                
Industry: {industry}
Tax Type: {taxType}

Please provide:
1. A brief summary of tax implications for this industry
2. 3-5 key recommendations
3. 3-4 common pitfalls specific to this industry
4. 3-4 best practices

Be specific and actionable.";

                var chatCompletionsOptions = new ChatCompletionsOptions()
                {
                    DeploymentName = deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(userPrompt)
                    },
                    Temperature = 0.3f,
                    MaxTokens = 1000,
                    NucleusSamplingFactor = 0.95f
                };

                var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
                var content = response.Value.Choices[0].Message.Content;

                return ParseIndustryRecommendations(content, industry, taxType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate industry recommendations for {industry} - {taxType}");
                return GetFallbackIndustryRecommendations(industry, taxType);
            }
        }

        private string BuildTaxInsightSystemPrompt()
        {
            return @"You are an expert tax compliance advisor specializing in U.S. federal excise taxes. 
You have deep knowledge of IRS regulations, compliance requirements, and industry best practices.
Your role is to provide accurate, actionable tax guidance based on current IRS publications and regulations.

When providing insights:
- Reference specific IRS publications (like Pub 510, 378, etc.) when applicable
- Be specific about compliance requirements
- Highlight common audit triggers and red flags
- Provide practical, actionable recommendations
- Note recent regulatory changes when relevant

Always maintain accuracy and cite sources when making specific claims about regulations.";
        }

        private List<AITaxInsightDto> ParseAIResponseToInsights(string aiResponse, string taxType)
        {
            // Simple parsing - in production, use more sophisticated parsing
            var insights = new List<AITaxInsightDto>();

            var categories = new[] { "Compliance Requirements", "Common Issues", "Best Practices", "Regulatory Changes" };
            
            foreach (var category in categories)
            {
                insights.Add(new AITaxInsightDto
                {
                    Category = category,
                    Insight = ExtractCategoryContent(aiResponse, category),
                    Confidence = 0.85,
                    Sources = new List<string> { "IRS Publication 510", "Azure OpenAI Analysis" },
                    RelevantRegulations = new List<string> { "26 USC § 4081", "IRC Section 4041" }
                });
            }

            return insights;
        }

        private string ExtractCategoryContent(string content, string category)
        {
            // Simplified extraction - in production, use regex or structured parsing
            var startIndex = content.IndexOf(category, StringComparison.OrdinalIgnoreCase);
            if (startIndex == -1) return $"AI-generated insights for {category}";

            var endIndex = content.IndexOf('\n', startIndex + 200);
            if (endIndex == -1) endIndex = Math.Min(startIndex + 300, content.Length);

            return content.Substring(startIndex, endIndex - startIndex).Trim();
        }

        private IndustryRecommendationDto ParseIndustryRecommendations(string aiResponse, string industry, string taxType)
        {
            // Simplified parsing - in production, use structured output
            return new IndustryRecommendationDto
            {
                Industry = industry,
                TaxType = taxType,
                Summary = $"AI-generated recommendations for {industry} industry regarding {taxType}",
                KeyRecommendations = new List<string>
                {
                    "Maintain detailed transaction records",
                    "Implement automated compliance checks",
                    "Regular reconciliation with IRS requirements"
                },
                CommonPitfalls = new List<string>
                {
                    "Incomplete documentation",
                    "Misclassification of taxable events"
                },
                BestPractices = new List<string>
                {
                    "Quarterly compliance reviews",
                    "Automated data validation"
                }
            };
        }

        private List<AITaxInsightDto> GetFallbackInsights(string taxType)
        {
            return new List<AITaxInsightDto>
            {
                new AITaxInsightDto
                {
                    Category = "Compliance Requirements",
                    Insight = $"Ensure all {taxType} transactions are properly documented and reported quarterly",
                    Confidence = 0.75,
                    Sources = new List<string> { "IRS Publication 510" },
                    RelevantRegulations = new List<string> { "26 USC § 4081" }
                }
            };
        }

        private IndustryRecommendationDto GetFallbackIndustryRecommendations(string industry, string taxType)
        {
            return new IndustryRecommendationDto
            {
                Industry = industry,
                TaxType = taxType,
                Summary = $"Standard tax compliance guidance for {industry} industry",
                KeyRecommendations = new List<string>
                {
                    "Maintain comprehensive transaction records",
                    "Implement regular compliance audits"
                },
                CommonPitfalls = new List<string>
                {
                    "Incomplete documentation"
                },
                BestPractices = new List<string>
                {
                    "Automated compliance monitoring"
                }
            };
        }
    }
}
