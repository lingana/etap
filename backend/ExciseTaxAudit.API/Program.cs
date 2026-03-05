using ExciseTaxAudit.API.Data;
using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Environment variables configuration
// Azure OpenAI credentials are loaded from environment variables:
// - AzureOpenAI__ApiKey
// - AzureOpenAI__Endpoint  
// - AzureOpenAI__DeploymentId
// See ENVIRONMENT_VARIABLES.md for setup instructions
builder.Configuration.AddEnvironmentVariables();

// Configure to use HTTP on both IPv4 and IPv6 on port 5000
builder.WebHost.UseUrls("http://0.0.0.0:5000", "http://[::]:5000");

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Use in-memory database for development (no SQL Server required)
builder.Services.AddDbContext<AuditContext>(options =>
    options.UseInMemoryDatabase("ExciseTaxAudit"));

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-secret-key-min-32-characters-long-please";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ExciseTaxAudit";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ExciseTaxAuditUsers";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Add caching for explanations
builder.Services.AddMemoryCache();

// Add HttpClient for Azure ML integration
builder.Services.AddHttpClient();

// Add application services
builder.Services.AddScoped<ExcelParsingService>();
builder.Services.AddScoped<AnomalyDetectionService>();
builder.Services.AddScoped<GenAIExplanationService>();
builder.Services.AddScoped<SyntheticDataService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ITaxClientService, TaxClientService>();

// Azure ML integration removed
builder.Services.AddScoped<IAnomalyExplanationService, AnomalyExplanationService>();
builder.Services.AddScoped<IPromptTemplateService, PromptTemplateService>();
builder.Services.AddSingleton<IAzureOpenAIConfigService, AzureOpenAIConfigService>();

// Add Power BI service
builder.Services.AddScoped<IPowerBIService, PowerBIService>();

// Add compliance services
builder.Services.AddScoped<ApprovalWorkflowService>();
builder.Services.AddScoped<AuditTrailService>();
builder.Services.AddScoped<ExportService>();

// Add refund and recovery services
builder.Services.AddScoped<RefundClaimService>();
builder.Services.AddScoped<Form8849GeneratorService>();

// Add Semantic Kernel plugins
builder.Services.AddSingleton<ExciseTaxAudit.API.Services.Plugins.TaxRatePlugin>();
builder.Services.AddSingleton<ExciseTaxAudit.API.Services.Plugins.CalculationValidatorPlugin>();
builder.Services.AddScoped<ExciseTaxAudit.API.Services.Plugins.HistoricalApprovalPlugin>();

// Configure Semantic Kernel with Azure OpenAI (lazy initialization to avoid startup failures)
builder.Services.AddSingleton<Microsoft.SemanticKernel.Kernel>(sp =>
{
    string? endpoint = null;
    string? deploymentName = null;
    string? apiKey = null;

    var logger = sp.GetRequiredService<ILogger<Program>>();
    var config = sp.GetRequiredService<IAzureOpenAIConfigService>();

    try
    {
        endpoint = config.GetEndpoint();
        apiKey = config.GetApiKey();
        deploymentName = config.GetDeploymentName();

        logger.LogInformation("🔧 Initializing Semantic Kernel - Endpoint: {Endpoint}, DeploymentId: {DeploymentId}", endpoint, deploymentName);

        var kernelBuilder = Microsoft.SemanticKernel.Kernel.CreateBuilder();

        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: deploymentName,
            endpoint: endpoint,
            apiKey: apiKey
        );

        // Register plugins using ImportPluginFromObject
        var kernel = kernelBuilder.Build();

        var taxRatePlugin = sp.GetRequiredService<ExciseTaxAudit.API.Services.Plugins.TaxRatePlugin>();
        var calculationPlugin = sp.GetRequiredService<ExciseTaxAudit.API.Services.Plugins.CalculationValidatorPlugin>();

        kernel.ImportPluginFromObject(taxRatePlugin, "TaxRate");
        kernel.ImportPluginFromObject(calculationPlugin, "Calculator");

        logger.LogInformation("✅ Semantic Kernel initialized successfully");
        return kernel;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ FAILED to initialize Semantic Kernel. Endpoint: {Endpoint}, DeploymentId: {DeploymentId}", endpoint ?? "UNKNOWN", deploymentName ?? "UNKNOWN");
        Console.WriteLine($"⚠️  WARNING: Failed to initialize Semantic Kernel: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        }
        Console.WriteLine("Agentic review features will be disabled. Check Azure OpenAI configuration.");

        // Return a kernel with the chat completion service registered (even if it might fail later)
        // This prevents "service not registered" errors in diagnostics
        var fallbackBuilder = Microsoft.SemanticKernel.Kernel.CreateBuilder();

        // Try to add the service anyway so diagnostics can test the actual connection
        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(deploymentName))
        {
            try
            {
                fallbackBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: deploymentName,
                    endpoint: endpoint,
                    apiKey: apiKey
                );
            }
            catch
            {
                // If even this fails, just return an empty kernel
                logger.LogWarning("Unable to register Azure OpenAI service even for diagnostics");
            }
        }

        return fallbackBuilder.Build();
    }
});

// Add Agentic Review Service
builder.Services.AddScoped<AgenticReviewService>();

// Add RAG Services for IRS regulatory compliance
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddSingleton<IRSConnectorToggleService>();
builder.Services.AddHttpClient<IRSDataConnectorService>();
builder.Services.AddScoped<IRSTaxRateRAGService>();

// Add IRS Data Service for real-time tax rate fetching
builder.Services.AddHttpClient<IIRSDataService, IRSDataService>();
builder.Services.AddScoped<IAITaxInsightService, AITaxInsightService>();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Excise Tax Audit API",
        Version = "v1",
        Description = "API for Excise Tax analytics and recovery with AI-powered insights for all tax types",
        Contact = new OpenApiContact
        {
            Name = "Development Team",
            Email = "dev@example.com"
        },
        License = new OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Add JWT Bearer authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
    
    // Include XML documentation
    var xmlFile = "ExciseTaxAudit.API.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add logging (console only, no EventLog to avoid permission issues)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger UI for all environments
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Excise Tax Audit API v1");
    c.RoutePrefix = "swagger"; // Swagger at: http://localhost:5000/swagger/index.html
});

// Skip HTTPS redirection in development
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowAngularApp");

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed database with synthetic data before starting
Console.WriteLine("🔄 Initializing database with synthetic data...");
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AuditContext>();
    var syntheticDataService = scope.ServiceProvider.GetRequiredService<SyntheticDataService>();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    
    // Seed synthetic data if empty
    if (!dbContext.TransactionRecords.Any())
    {
        var syntheticRecords = syntheticDataService.GenerateSyntheticData();
        dbContext.TransactionRecords.AddRange(syntheticRecords);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"✅ Database seeded with {syntheticRecords.Count} records");
    }
    else
    {
        Console.WriteLine($"✅ Database already contains records");
    }

    // Seed demo users if empty
    if (!dbContext.Users.Any())
    {
        var demoUsers = new[]
        {
            new User
            {
                Username = "auditor",
                Email = "auditor@excise.local",
                FirstName = "Naveen",
                LastName = "L",
                PasswordHash = authService.HashPassword("password123"),
                Role = UserRole.Auditor,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "manager",
                Email = "manager@excise.local",
                FirstName = "Sarah",
                LastName = "Johnson",
                PasswordHash = authService.HashPassword("password123"),
                Role = UserRole.Manager,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "admin",
                Email = "admin@excise.local",
                FirstName = "Mike",
                LastName = "Brown",
                PasswordHash = authService.HashPassword("password123"),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        dbContext.Users.AddRange(demoUsers);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"✅ Demo users created (auditor, manager, admin - all use password 'password123')");
    }
    else
    {
        Console.WriteLine($"✅ Users already exist in database");
    }

    // Seed tax types
    if (!dbContext.TaxTypes.Any())
    {
        var taxTypes = new[]
        {
            new TaxType
            {
                Type = TaxTypeEnum.FuelTax,
                Name = "Fuel Tax",
                Description = "Federal excise tax on gasoline, diesel, and other motor fuels",
                Icon = "local_gas_station",
                TaxRate = 0.184m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new TaxType
            {
                Type = TaxTypeEnum.HUVT,
                Name = "Heavy Vehicle Use Tax (HUVT)",
                Description = "Tax on the use of heavy vehicles on public roads",
                Icon = "local_shipping",
                TaxRate = 0.055m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new TaxType
            {
                Type = TaxTypeEnum.AlcoholTax,
                Name = "Alcohol Tax",
                Description = "Federal excise tax on beer, wine, and spirits",
                Icon = "local_bar",
                TaxRate = 0.105m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new TaxType
            {
                Type = TaxTypeEnum.TobaccoTax,
                Name = "Tobacco Tax",
                Description = "Federal excise tax on cigarettes and cigars",
                Icon = "smoking_rooms",
                TaxRate = 1.006m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new TaxType
            {
                Type = TaxTypeEnum.OtherExciseTax,
                Name = "Other Excise Taxes",
                Description = "Various other federal excise taxes",
                Icon = "calculate",
                TaxRate = 0.05m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        dbContext.TaxTypes.AddRange(taxTypes);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"✅ Tax types seeded ({taxTypes.Length} records)");

        // Reload tax types from DB to get proper IDs
        var savedTaxTypes = await dbContext.TaxTypes.ToListAsync();

        // Seed clients
        var managerUser = dbContext.Users.FirstOrDefault(u => u.Role == UserRole.Manager);
        var clients = new[]
        {
            // Fuel Tax Clients
            new Client
            {
                Name = "Shell Oil Company",
                EIN = "12-3456789",
                Industry = "Petroleum Refining",
                ContactPerson = "John Smith",
                ContactEmail = "john.smith@shell.com",
                ContactPhone = "(832) 123-4567",
                Address = "1 Shell Plaza",
                City = "Houston",
                State = "TX",
                ZipCode = "77002",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.FuelTax).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Client
            {
                Name = "Chevron Corporation",
                EIN = "12-3456790",
                Industry = "Petroleum Refining",
                ContactPerson = "Jane Doe",
                ContactEmail = "jane.doe@chevron.com",
                ContactPhone = "(925) 842-1000",
                Address = "6001 Bollinger Canyon Rd",
                City = "San Ramon",
                State = "CA",
                ZipCode = "94583",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.FuelTax).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Client
            {
                Name = "ExxonMobil Corp",
                EIN = "12-3456791",
                Industry = "Petroleum Refining",
                ContactPerson = "Robert Johnson",
                ContactEmail = "robert.johnson@exxonmobil.com",
                ContactPhone = "(972) 444-1000",
                Address = "5959 Las Colinas Blvd",
                City = "Irving",
                State = "TX",
                ZipCode = "75039",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.FuelTax).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            // HUVT Clients
            new Client
            {
                Name = "J.B. Hunt Transport Services",
                EIN = "12-3456792",
                Industry = "Trucking",
                ContactPerson = "Michael Lee",
                ContactEmail = "mlee@jbhunt.com",
                ContactPhone = "(479) 820-0000",
                Address = "615 J.B. Hunt Way",
                City = "Lowell",
                State = "AR",
                ZipCode = "72745",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.HUVT).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Client
            {
                Name = "Knight-Swift Transportation",
                EIN = "12-3456793",
                Industry = "Trucking",
                ContactPerson = "Patricia Martin",
                ContactEmail = "pmartin@knight-swift.com",
                ContactPhone = "(602) 263-2000",
                Address = "500 W. Madison St",
                City = "Phoenix",
                State = "AZ",
                ZipCode = "85003",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.HUVT).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            // Alcohol Tax Clients
            new Client
            {
                Name = "Anheuser-Busch InBev",
                EIN = "12-3456794",
                Industry = "Beverage Manufacturing",
                ContactPerson = "David Wilson",
                ContactEmail = "dwilson@ab-inbev.com",
                ContactPhone = "(314) 577-2000",
                Address = "1 Busch Place",
                City = "St. Louis",
                State = "MO",
                ZipCode = "63118",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.AlcoholTax).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Client
            {
                Name = "Miller Coors LLC",
                EIN = "12-3456795",
                Industry = "Beverage Manufacturing",
                ContactPerson = "Elizabeth Brown",
                ContactEmail = "ebrown@millercoors.com",
                ContactPhone = "(303) 433-3711",
                Address = "250 Park Lane",
                City = "Broomfield",
                State = "CO",
                ZipCode = "80021",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.AlcoholTax).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            // Tobacco Tax Clients
            new Client
            {
                Name = "Philip Morris USA",
                EIN = "12-3456796",
                Industry = "Tobacco Manufacturing",
                ContactPerson = "Christopher Davis",
                ContactEmail = "cdavis@philipmorrisusa.com",
                ContactPhone = "(804) 274-2000",
                Address = "4001 Hanover Ave",
                City = "Richmond",
                State = "VA",
                ZipCode = "23218",
                TaxTypeId = savedTaxTypes.First(t => t.Type == TaxTypeEnum.TobaccoTax).Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        dbContext.Clients.AddRange(clients);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"✅ Clients seeded ({clients.Length} records)");

        // Reload clients from DB to get proper IDs
        var savedClients = await dbContext.Clients.ToListAsync();

        // Seed engagements
        var engagements = new List<Engagement>();
        var random = new Random(42);
        var startYear = 2022;

        foreach (var client in savedClients)
        {
            // Create 2-3 engagements per client
            int engagementCount = random.Next(2, 4);
            for (int i = 0; i < engagementCount; i++)
            {
                var fiscalYear = startYear + i;
                var types = new[] {
                    EngagementTypeEnum.FullAudit,
                    EngagementTypeEnum.LimitedScope,
                    EngagementTypeEnum.Review,
                    EngagementTypeEnum.Consultation
                };

                // Balanced status distribution based on engagement count
                // 25% Completed, 30% InProgress, 25% UnderReview, 20% Planning
                var statusIndex = engagements.Count % 20;
                var status = statusIndex switch
                {
                    < 5 => EngagementStatusEnum.Completed,      // 25%
                    < 11 => EngagementStatusEnum.InProgress,    // 30%
                    < 16 => EngagementStatusEnum.UnderReview,   // 25%
                    _ => EngagementStatusEnum.Planning          // 20%
                };

                // Use a stable, human-friendly external ID for demos.
                // Real systems typically use a separate business identifier, not the DB primary key.
                var externalId = (2342763482L + engagements.Count).ToString();

                engagements.Add(new Engagement
                {
                    ClientId = client.Id,
                    ExternalEngagementId = externalId,
                    EngagementName = $"{client.Name} - FY{fiscalYear} {types[random.Next(types.Length)]}",
                    FiscalYear = fiscalYear,
                    FiscalYearStart = new DateTime(fiscalYear, 1, 1),
                    FiscalYearEnd = new DateTime(fiscalYear, 12, 31),
                    EngagementType = types[random.Next(types.Length)],
                    Status = status,
                    Description = $"Tax audit engagement for fiscal year {fiscalYear}",
                    LeadAuditorId = managerUser?.Id,
                    BudgetedHours = random.Next(100, 400),
                    ActualHours = random.Next(80, 380),
                    EstimatedPotentialRecovery = decimal.Round((decimal)random.Next(50000, 500000), 2),
                    ActualRecovery = decimal.Round((decimal)random.Next(30000, 400000), 2),
                    Notes = $"Audit for {client.Name} covering FY{fiscalYear}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        dbContext.Engagements.AddRange(engagements);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"✅ Engagements seeded ({engagements.Count} records)");
    }
    else
    {
        Console.WriteLine($"✅ Tax types, clients, and engagements already exist");
    }
}


Console.WriteLine($"🚀 Server starting on http://localhost:5000");
Console.WriteLine($"📊 Swagger UI at http://localhost:5000/swagger/index.html");
await app.RunAsync();
