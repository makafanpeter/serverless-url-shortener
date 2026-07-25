# Serverless URL Shortener

A production-ready URL shortener built with **ASP.NET Core (.NET 8)** running on **AWS Lambda**, exposed via **Amazon API Gateway**, and backed by **Amazon DynamoDB**.

---

## Architecture

```
Client
  │
  ▼
Amazon API Gateway (REST API)
  │
  ▼
AWS Lambda  ─────────────────────────────────────────────────┐
  │  ASP.NET Core (Amazon.Lambda.AspNetCoreServer.Hosting)   │
  │                                                           │
  ├── GET  /{code}                → Redirect (302)           │
  ├── POST /api/url-shortener     → Create short URL         │
  ├── GET  /api/url-shortener/{code} → Get metadata          │
  ├── DEL  /api/url-shortener/{code} → Delete short URL      │
  └── GET  /stats/{code}          → Analytics snapshot       │
                                                             │
Amazon DynamoDB  ◄────────────────────────────────────────────┘
  Table: UrlShortener
  Hash key: ShortCode (String)
  Billing: PAY_PER_REQUEST
```

---

## Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| AWS CLI | v2 | https://aws.amazon.com/cli/ |
| Amazon.Lambda.Tools | latest | `dotnet tool install -g Amazon.Lambda.Tools` |
| AWS SAM CLI *(optional)* | latest | https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html |

Verify your toolchain:

```bash
dotnet --version          # 8.0+
aws --version             # aws-cli/2.x
dotnet lambda --version   # Amazon Lambda Tools
```

---

## Project Structure

```
serverless-url-shortener/
└── src/
    └── API/
        └── UrlShortener/
            ├── Controllers/
            │   ├── UrlShortenerController.cs   # CRUD endpoints
            │   └── StatsController.cs          # Analytics endpoint
            ├── Exceptions/
            │   ├── AppException.cs             # Base exception (status + error code)
            │   ├── NotFoundException.cs        # 404
            │   ├── ConflictException.cs        # 409
            │   └── ValidationException.cs      # 400
            ├── Infrastructure/
            │   └── GlobalExceptionHandler.cs   # Centralised error → JSON mapping
            ├── Models/
            │   ├── CreateShortUrlRequest.cs
            │   ├── ShortUrlResponse.cs
            │   ├── UrlStatsResponse.cs
            │   └── ErrorResponse.cs
            ├── Persistence/
            │   ├── Entities/UrlRecord.cs       # DynamoDB entity
            │   └── Infrastructure/
            │       ├── IDynamoDbContext.cs
            │       └── DynamoDbContext.cs      # High-level + low-level DynamoDB client
            ├── Services/
            │   ├── IUrlShortenerService.cs
            │   └── UrlShortenerService.cs      # Base62 code gen, atomic PutItem
            ├── Program.cs
            ├── appsettings.json
            ├── aws-lambda-tools-defaults.json  # Lambda CLI defaults
            └── serverless.template             # SAM / CloudFormation template
```

---

## Configuration

All configuration lives in `appsettings.json` and can be overridden with **environment variables** (using double-underscore `__` as the section separator).

| Key | Environment Variable | Default | Description |
|-----|---------------------|---------|-------------|
| `AmazonOptions:RegionEndpoint` | `AmazonOptions__RegionEndpoint` | `eu-west-1` | AWS region for DynamoDB |
| `AmazonOptions:TablePrefix` | `AmazonOptions__TablePrefix` | *(empty)* | Optional prefix for the DynamoDB table name (e.g. `dev-`, `prod-`) |

> [!NOTE]
> The SAM template injects `AmazonOptions__RegionEndpoint` and `AmazonOptions__TablePrefix` automatically as Lambda environment variables, so you do not need to set them manually after deployment.

---

## Deployment

### 1 — Configure AWS credentials

```bash
aws configure
# Enter your Access Key ID, Secret Access Key, region (e.g. eu-west-1), and output format (json)
```

Or use a named profile:

```bash
aws configure --profile my-profile
```

### 2 — Create an S3 bucket for deployment artefacts

Lambda deployment packages are staged in S3 before CloudFormation picks them up.

```bash
aws s3 mb s3://my-url-shortener-deployments --region eu-west-1
```

### 3 — Deploy

Run from the project directory:

```bash
cd src/API/UrlShortener

dotnet lambda deploy-serverless \
  --stack-name url-shortener \
  --s3-bucket my-url-shortener-deployments \
  --region eu-west-1
```

The command will:
1. Build and publish the .NET project in Release mode
2. Package and upload the artefact to S3
3. Run `aws cloudformation deploy` to create/update the stack
4. Print the API Gateway URL on success

#### Deploying to multiple environments

Use the `TablePrefix` parameter to isolate DynamoDB tables per environment:

```bash
# Staging
dotnet lambda deploy-serverless \
  --stack-name url-shortener-staging \
  --s3-bucket my-url-shortener-deployments \
  --template-parameters TablePrefix=staging-

# Production
dotnet lambda deploy-serverless \
  --stack-name url-shortener-prod \
  --s3-bucket my-url-shortener-deployments \
  --template-parameters TablePrefix=prod-
```

### 4 — Get the API URL

After deployment the CloudFormation stack outputs the endpoint:

```bash
aws cloudformation describe-stacks \
  --stack-name url-shortener \
  --query "Stacks[0].Outputs[?OutputKey=='ApiURL'].OutputValue" \
  --output text
```

---

## API Reference

Base URL: `https://{api-id}.execute-api.{region}.amazonaws.com/Prod`

### Create a short URL

```http
POST /api/url-shortener
Content-Type: application/json

{
  "longUrl": "https://www.example.com/very/long/path",
  "customAlias": "my-alias"   // optional; 3–50 chars, letters/digits/hyphens
}
```

**201 Created**
```json
{
  "shortCode": "my-alias",
  "shortUrl": "https://.../my-alias",
  "longUrl": "https://www.example.com/very/long/path",
  "clicks": 0,
  "createdAt": "2026-07-25T18:00:00+00:00",
  "lastAccessed": "0001-01-01T00:00:00+00:00"
}
```

---

### Follow a short URL *(redirect)*

```http
GET /{code}
```

**302 Found** → redirects to the original URL and increments the click counter.

---

### Get metadata

```http
GET /api/url-shortener/{code}
```

**200 OK** — same shape as the create response. Does **not** increment clicks.

---

### Get analytics

```http
GET /stats/{code}
```

**200 OK**
```json
{
  "shortCode": "my-alias",
  "shortUrl": "https://.../my-alias",
  "longUrl": "https://www.example.com/very/long/path",
  "totalClicks": 42,
  "createdAt": "2026-07-01T10:00:00+00:00",
  "lastAccessedAt": "2026-07-25T18:30:00+00:00",
  "ageInDays": 24,
  "averageClicksPerDay": 1.75,
  "isActive": true
}
```

| Field | Description |
|-------|-------------|
| `ageInDays` | Full days since the link was created |
| `averageClicksPerDay` | `totalClicks ÷ ageInDays` (0 if < 1 day old) |
| `isActive` | `true` if accessed at least once in the last 30 days |
| `lastAccessedAt` | `null` if the link has never been followed |

---

### Delete a short URL

```http
DELETE /api/url-shortener/{code}
```

**204 No Content** on success.

---

### Error responses

All errors share a consistent envelope:

```json
{
  "code": "NOT_FOUND",
  "message": "Short code 'xyz' not found.",
  "traceId": "0HMXXXX:00000001"
}
```

| `code` | HTTP | Cause |
|--------|------|-------|
| `VALIDATION_ERROR` | 400 | Invalid input (bad URL, invalid alias format) |
| `NOT_FOUND` | 404 | Short code does not exist |
| `CONFLICT` | 409 | Custom alias is already taken |
| `SHORT_CODE_EXHAUSTED` | 503 | Could not generate a unique code after retries |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

---

## Local Development

Run the API locally using the standard .NET dev server (Kestrel):

```bash
cd src/API/UrlShortener
dotnet run
```

> [!IMPORTANT]
> Running locally requires **real AWS credentials** with DynamoDB access, as there is no embedded DynamoDB emulator configured. You can use [DynamoDB Local](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html) or [LocalStack](https://www.localstack.cloud/) and update the endpoint in `appsettings.Development.json`.

To test with the **AWS Lambda Mock Test Tool** (simulates API Gateway → Lambda locally):

```bash
cd src/API/UrlShortener
dotnet run --launch-profile "Mock Lambda Test Tool"
```

---

## Teardown

To remove all AWS resources created by the stack:

```bash
aws cloudformation delete-stack --stack-name url-shortener --region eu-west-1
```

> [!CAUTION]
> This permanently deletes the DynamoDB table and all short URL records. Back up the data first if needed.
