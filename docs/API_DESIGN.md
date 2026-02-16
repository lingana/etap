# Excise Tax Audit System - API Design Document

## Overview

This document describes the RESTful API design for the Excise Tax Audit system, including endpoint specifications, request/response formats, and integration points.

---

## Base URL

- **Development:** `http://localhost:5000/api`
- **Production:** `https://api.exciseTaxAudit.azure.com/api`

---

## Authentication

Currently using basic CORS. **Phase 2** will integrate Azure AD.

```
Authorization: Bearer <token>
```

---

## Endpoints

### 1. Upload Management

#### 1.1 Upload Excel File

**Request:**
```http
POST /upload/excel
Content-Type: multipart/form-data

file: <binary>
```

**Response (200 OK):**
```json
{
  "jobId": "uuid-string",
  "fileName": "transactions_202201.xlsx",
  "uploadedAt": "2025-01-15T10:30:00Z",
  "status": "Processing",
  "totalRecords": 5000,
  "processedRecords": 0,
  "flaggedRecords": 0,
  "errorMessage": null
}
```

---

#### 1.2 Get Upload Status

**Request:**
```http
GET /upload/{jobId}/status
```

**Response (200 OK):**
```json
{
  "jobId": "uuid-string",
  "fileName": "transactions_202201.xlsx",
  "uploadedAt": "2025-01-15T10:30:00Z",
  "status": "Completed",
  "totalRecords": 5000,
  "processedRecords": 5000,
  "flaggedRecords": 127,
  "errorMessage": null,
  "sourceBlobUrl": "https://storage.blob.core.windows.net/uploads/..."
}
```

---

#### 1.3 Preview File

**Request:**
```http
POST /upload/preview
Content-Type: multipart/form-data

file: <binary>
maxRows: 10
```

**Response (200 OK):**
```json
[
  {
    "recordID": 616924,
    "transactionNumber": "000136100",
    "transactionDate": "2022-01-03T00:00:00Z",
    "merchantName": "ONE9 EZ TRIP 1277",
    "merchantCity": "AVENAL",
    "merchantState": "CA",
    "fuelType": "DIESEL",
    "quantity": 87.0,
    "pricePerUnit": 4.5591,
    "netCost": 396.64
  }
]
```

---

### 2. Transaction Queries

#### 2.1 Get Flagged Transactions

**Request:**
```http
GET /transactions/flagged?scoreThreshold=0.5&pageSize=50&pageNumber=1
```

**Query Parameters:**
| Param | Type | Default | Description |
|-------|------|---------|-------------|
| scoreThreshold | float | 0.5 | Minimum anomaly score to include |
| pageSize | int | 50 | Records per page |
| pageNumber | int | 1 | Page number (1-indexed) |

**Response (200 OK):**
```json
[
  {
    "recordID": 616924,
    "transactionNumber": "000136100",
    "transactionDate": "2022-01-03T00:00:00Z",
    "merchantName": "ONE9 EZ TRIP 1277",
    "merchantState": "CA",
    "fuelType": "DIESEL",
    "quantity": 87.0,
    "pricePerUnit": 4.5591,
    "netCost": 396.64,
    "totalTaxAmount": 0.0,
    "anomalyScore": 0.89,
    "anomalyReason": "Zero tax on high-value transaction (potential underpayment)",
    "expectedTaxAmount": 64.50,
    "taxDifference": -64.50,
    "predictedClaimType": "UNDER",
    "isReviewed": false,
    "auditorNotes": null
  }
]
```

---

#### 2.2 Get Transaction Detail

**Request:**
```http
GET /transactions/{recordId}
```

**Path Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| recordId | long | Transaction record ID |

**Response (200 OK):**
```json
{
  "recordID": 616924,
  "transactionNumber": "000136100",
  "transactionDate": "2022-01-03T00:00:00Z",
  "merchantName": "ONE9 EZ TRIP 1277",
  "merchantState": "CA",
  "fuelType": "DIESEL",
  "quantity": 87.0,
  "pricePerUnit": 4.5591,
  "netCost": 396.64,
  "totalTaxAmount": 0.0,
  "anomalyScore": 0.89,
  "anomalyReason": "Zero tax on high-value transaction (potential underpayment)",
  "expectedTaxAmount": 64.50,
  "taxDifference": -64.50,
  "predictedClaimType": "UNDER",
  "explanationText": "AUDIT FINDINGS FOR TRANSACTION 000136100\n...",
  "isReviewed": false,
  "auditorNotes": null
}
```

**Response (404 Not Found):**
```json
{
  "error": "Transaction not found"
}
```

---

#### 2.3 Review Transaction

**Request:**
```http
POST /transactions/{recordId}/review
Content-Type: application/json

{
  "decision": "UNDER",
  "notes": "Verified underpayment. Eligible for refund claim.",
  "adjustmentAmount": 64.50
}
```

**Request Body:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| decision | string | Yes | OVER, UNDER, or OK |
| notes | string | No | Auditor notes |
| adjustmentAmount | decimal | No | Adjustment amount in dollars |

**Response (200 OK):**
```json
{
  "message": "Transaction reviewed successfully."
}
```

---

#### 2.4 Get Dashboard Statistics

**Request:**
```http
GET /transactions/summary/stats
```

**Response (200 OK):**
```json
{
  "totalRecords": 5000,
  "flaggedRecords": 127,
  "reviewedRecords": 45,
  "flaggingRate": 0.0254,
  "estimatedOverPayments": 12345.67,
  "estimatedUnderPayments": 8901.23,
  "potentialRecovery": 21246.90
}
```

---

## Error Handling

### HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | OK - Request succeeded |
| 400 | Bad Request - Invalid parameters |
| 401 | Unauthorized - Missing/invalid auth |
| 404 | Not Found - Resource doesn't exist |
| 500 | Internal Server Error |

### Error Response Format

```json
{
  "error": "Error message",
  "details": "Additional details if available",
  "timestamp": "2025-01-15T10:30:00Z"
}
```

---

## Data Models

### TransactionRecord

```typescript
interface TransactionRecord {
  recordID: number;
  branch: string;
  department: string;
  entity: string;
  transactionNumber: string;
  billingCode: string;
  cardNumberMask: string;
  fuelPlatform: string;
  customerID: string;
  fuelType: string;
  discrepancies: string;
  employeeID: string;
  employeeType: string;
  transactionDate: string; // ISO 8601
  merchantName: string;
  merchantCity: string;
  merchantState: string;
  pADDRegion: string;
  assetNumber: string;
  assetDescription: string;
  odometer: number;
  productDescription: string;
  productType: string;
  quantity: number;
  pricePerUnit: number;
  netCost: number;
  grossCost: number;
  uOM: string;
  postedDate: string; // ISO 8601
  month: number;
  currency: string;
  cardTypeFlag: string;
  cardholderName: string;
  tripDispatchQuantity: number;
  reportingLevel: string;
  mCC: string;
  merchantCode: string;
  merchantAddress1: string;
  merchantAddress2: string;
  merchantPostalCode: string;
  chainCode: number;
  crossBorderTransactionAmount: number;
  totalAmountDue: number;
  totalDiscountAmount: number;
  totalTaxAmount: number;
  transactionFee: number;
  fuelTransactionID: number;
  fuelTransactionDetailID: number;
  branchDescription: string;
  originalBranch: string;
  originalBranchDescription: string;
  reviewedBy: string;
  reviewedDate: string; // ISO 8601
  reviewedComments: string;
  paymentProcessedBy: string;
  paymentProcessedDate: string; // ISO 8601
  transactionTimezone: string;
  lastModifiedBy: string;
  lastModifiedDate: string; // ISO 8601
  fileName: string;
  k_EquipmentDescription: string;
  k_SafeHarborPercentage: number;
  ingestedAt: string; // ISO 8601
  sourceFileUrl: string;
  label_OverUnder: string; // OVER, UNDER, OK
  label_AdjustmentAmount: number;
  anomalyScore: number; // 0-1
  anomalyReason: string;
  isReviewed: boolean;
  auditorNotes: string;
}
```

---

## Rate Limiting

Not currently implemented. Will be added in Phase 2 for production.

---

## Versioning

Current API version: **v1**

Future versions will be available at:
- `/api/v2/...`
- `/api/v3/...`

---

## Integration Examples

### cURL

```bash
# Upload file
curl -X POST http://localhost:5000/api/upload/excel \
  -F "file=@transactions.xlsx"

# Get flagged transactions
curl -X GET "http://localhost:5000/api/transactions/flagged?scoreThreshold=0.6"

# Review a transaction
curl -X POST http://localhost:5000/api/transactions/616924/review \
  -H "Content-Type: application/json" \
  -d '{
    "decision": "UNDER",
    "notes": "Verified underpayment",
    "adjustmentAmount": 64.50
  }'
```

### JavaScript/TypeScript

```typescript
// Upload file
const formData = new FormData();
formData.append('file', fileInput.files[0]);

const response = await fetch('http://localhost:5000/api/upload/excel', {
  method: 'POST',
  body: formData
});

const result = await response.json();
console.log(result.jobId);
```

---

## Swagger/OpenAPI

Interactive API documentation available at:
```
http://localhost:5000/swagger
```

---

*Last Updated: January 2025*
