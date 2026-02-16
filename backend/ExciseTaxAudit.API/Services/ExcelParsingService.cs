using ExciseTaxAudit.API.Models;
using ExcelDataReader;
using System.Text;

namespace ExciseTaxAudit.API.Services;

/// <summary>
/// Service for parsing and validating Excel/XLSX files containing fuel transaction data.
/// IMPORTANT: Column indices must match business-provided CSV schema exactly (0-based indexing).
/// Reference: sample-transactions.csv provided by business
/// </summary>
public class ExcelParsingService
{
    private readonly ILogger<ExcelParsingService> _logger;

    /// <summary>
    /// Initialize ExcelParsingService with logger.
    /// </summary>
    public ExcelParsingService(ILogger<ExcelParsingService> logger)
    {
        _logger = logger;
        // ExcelDataReader requires encoding provider
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Parse Excel file into a list of TransactionRecord objects.
    /// </summary>
    public async Task<List<TransactionRecord>> ParseExcelAsync(Stream fileStream)
    {
        return await Task.Run(() => ParseExcelInternal(fileStream));
    }

    private List<TransactionRecord> ParseExcelInternal(Stream fileStream)
    {
        var records = new List<TransactionRecord>();

        try
        {
            using (var reader = ExcelReaderFactory.CreateReader(fileStream))
            {
                reader.Read(); // Skip header row
                int rowCount = 0;

                while (reader.Read())
                {
                    rowCount++;
                    try
                    {
                        var record = ParseRow(reader);
                        if (record != null)
                        {
                            records.Add(record);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error parsing row {rowCount}: {ex.Message}");
                    }
                }

                if (rowCount == 0)
                {
                    _logger.LogWarning("Excel file has no data rows.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reading Excel file: {ex.Message}");
            throw;
        }

        return records;
    }

    private TransactionRecord? ParseRow(IExcelDataReader reader)
    {
        try
        {
            return new TransactionRecord
            {
                RecordID = GetLongValue(reader, 0) ?? 0,
                Branch = GetStringValue(reader, 1),
                Department = GetStringValue(reader, 2),
                Entity = GetStringValue(reader, 3),
                TransactionNumber = GetStringValue(reader, 4),
                BillingCode = GetStringValue(reader, 5),
                CardNumberMask = MaskCardNumber(GetStringValue(reader, 6)),
                FuelPlatform = GetStringValue(reader, 7),
                CustomerID = GetStringValue(reader, 8),
                FuelType = GetStringValue(reader, 9),
                Discrepancies = GetStringValue(reader, 10),
                EmployeeID = GetStringValue(reader, 11),
                EmployeeType = GetStringValue(reader, 12),
                TransactionDate = GetDateTimeValue(reader, 13) ?? DateTime.UtcNow,
                MerchantName = GetStringValue(reader, 14),
                MerchantCity = GetStringValue(reader, 15),
                MerchantState = GetStringValue(reader, 16),
                PADDRegion = GetStringValue(reader, 17),
                AssetNumber = GetStringValue(reader, 18),
                AssetDescription = GetStringValue(reader, 19),
                Odometer = GetLongValue(reader, 20),
                ProductDescription = GetStringValue(reader, 21),
                ProductType = GetStringValue(reader, 22),
                Quantity = GetDecimalValue(reader, 23) ?? 0,
                PricePerUnit = GetDecimalValue(reader, 24) ?? 0,
                NetCost = GetDecimalValue(reader, 25) ?? 0,
                GrossCost = GetDecimalValue(reader, 26) ?? 0,
                UOM = GetStringValue(reader, 27),
                PostedDate = GetDateTimeValue(reader, 28) ?? DateTime.UtcNow,
                Month = GetIntValue(reader, 29) ?? 1,
                Currency = GetStringValue(reader, 30),
                CardTypeFlag = GetStringValue(reader, 31),
                CardholderName = GetStringValue(reader, 32),
                TripDispatchQuantity = GetDecimalValue(reader, 33),
                ReportingLevel = GetStringValue(reader, 34),
                MCC = GetStringValue(reader, 35),
                MerchantCode = GetStringValue(reader, 36),
                MerchantAddress1 = GetStringValue(reader, 37),
                MerchantAddress2 = GetStringValue(reader, 38),
                MerchantPostalCode = GetStringValue(reader, 39),
                ChainCode = GetIntValue(reader, 40),
                PSItemID = GetStringValue(reader, 41),
                CrossBorderTransaction = GetStringValue(reader, 42),
                TotalAmountDue = GetDecimalValue(reader, 43) ?? 0,
                TotalDiscountAmount = GetDecimalValue(reader, 44) ?? 0,
                TotalTaxAmount = GetDecimalValue(reader, 45) ?? 0,
                TransactionFee = GetDecimalValue(reader, 46) ?? 0,
                FuelTransactionID = GetLongValue(reader, 47),
                FuelTransactionDetailID = GetLongValue(reader, 48),
                BranchDescription = GetStringValue(reader, 49),
                OriginalBranch = GetStringValue(reader, 50),
                OriginalBranchDescription = GetStringValue(reader, 51),
                ReviewedBy = GetStringValue(reader, 52),
                ReviewedDate = GetDateTimeValue(reader, 53),
                ReviewedComments = GetStringValue(reader, 54),
                PaymentProcessedBy = GetStringValue(reader, 55),
                PaymentProcessedDate = GetDateTimeValue(reader, 56),
                TransactionTimezone = GetStringValue(reader, 57),
                LastModifiedBy = GetStringValue(reader, 58),
                LastModifiedDate = GetDateTimeValue(reader, 59),
                FileName = GetStringValue(reader, 60),
                FullCardNumber = GetStringValue(reader, 61), // Will be empty for PII protection
                K_EquipmentDescription = GetStringValue(reader, 62),
                K_SafeHarborPercentage = GetDecimalValue(reader, 63),
                IngestedAt = DateTime.UtcNow,
                IsReviewed = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error mapping row: {ex.Message}");
            return null;
        }
    }

    private string? GetStringValue(IExcelDataReader reader, int columnIndex)
    {
        var value = reader.GetValue(columnIndex);
        return value?.ToString()?.Trim();
    }

    private decimal? GetDecimalValue(IExcelDataReader reader, int columnIndex)
    {
        var value = reader.GetValue(columnIndex);
        if (value == null) return null;
        return decimal.TryParse(value.ToString() ?? "", out var result) ? result : null;
    }

    private long? GetLongValue(IExcelDataReader reader, int columnIndex)
    {
        var value = reader.GetValue(columnIndex);
        if (value == null) return null;
        return long.TryParse(value.ToString() ?? "", out var result) ? result : null;
    }

    private int? GetIntValue(IExcelDataReader reader, int columnIndex)
    {
        var value = reader.GetValue(columnIndex);
        if (value == null) return null;
        return int.TryParse(value.ToString() ?? "", out var result) ? result : null;
    }

    private DateTime? GetDateTimeValue(IExcelDataReader reader, int columnIndex)
    {
        var value = reader.GetValue(columnIndex);
        if (value == null) return null;
        if (DateTime.TryParse(value.ToString() ?? "", out var result))
        {
            return result;
        }
        return null;
    }

    /// <summary>
    /// Mask credit card number (show last 4 digits only).
    /// </summary>
    private string? MaskCardNumber(string? cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            return cardNumber;

        return $"****{cardNumber.Substring(cardNumber.Length - 4)}";
    }
}
