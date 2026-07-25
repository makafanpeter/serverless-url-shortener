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
| AWS SAM CLI | latest | https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html |
| Docker *(optional, for `sam local`)* | latest | https://www.docker.com/products/docker-desktop |

Verify your toolchain:

```bash
dotnet --version   # 8.0+
aws --version      # aws-cli/2.x
sam --version      # SAM CLI 1.x+
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
# Enter your Access Key ID, Secret Access Key, default region (e.g. eu-west-1), and output format (json)
```

Or use a named profile and export it for the session:

```bash
aws configure --profile my-profile
export AWS_PROFILE=my-profile
```

### 2 — Build

Run from the **project** directory (where `serverless.template` lives):

```bash
cd src/API/UrlShortener
sam build --template serverless.template
```

SAM compiles the .NET project in Release mode and stages the output under `.aws-sam/build/`.

### 3 — Deploy (first time)

```bash
sam deploy --guided --template-file serverless.template
```

The interactive wizard will ask for:

| Prompt | Suggested value |
|--------|-----------------|
| Stack name | `url-shortener` |
| AWS Region | `eu-west-1` |
| Parameter `TablePrefix` | *(leave blank for default, or enter e.g. `dev-`)* |
| Confirm changeset | `Y` |
| Allow SAM to create IAM roles | `Y` |
| Save config to `samconfig.toml` | `Y` |

The answers are saved to `samconfig.toml` — subsequent deploys only need:

```bash
sam build --template serverless.template && sam deploy
```

### 4 — Deploying to multiple environments

Use the `TablePrefix` parameter to give each environment its own DynamoDB table.
You can maintain separate `samconfig.toml` configs using SAM config environments:

```bash
# Staging
sam deploy \
  --config-env staging \
  --stack-name url-shortener-staging \
  --parameter-overrides TablePrefix=staging-

# Production
sam deploy \
  --config-env prod \
  --stack-name url-shortener-prod \
  --parameter-overrides TablePrefix=prod-
```

### 5 — Get the API URL

The URL is printed at the end of every `sam deploy`. You can also retrieve it at any time:

```bash
sam list stack-outputs --stack-name url-shortener --output table
```

Or directly via the AWS CLI:

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

### Option A — Kestrel (fastest)

```bash
cd src/API/UrlShortener
dotnet run
```

The API is available at `http://localhost:5000`.

> [!IMPORTANT]
> This requires **real AWS credentials** with DynamoDB access. To avoid hitting a live table, use [DynamoDB Local](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBLocal.html) or [LocalStack](https://www.localstack.cloud/) and override the endpoint in `appsettings.Development.json`.

### Option B — SAM Local (simulates API Gateway + Lambda)

Requires Docker.

```bash
cd src/API/UrlShortener

# Build first
sam build --template serverless.template

# Start a local API Gateway emulator on http://localhost:3000
sam local start-api --template serverless.template
```

Test a redirect:
```bash
curl -v http://localhost:3000/my-alias
```

### Viewing logs

Tail live Lambda logs after deployment:

```bash
sam logs --stack-name url-shortener --tail
```

Filter to a specific function:

```bash
sam logs --stack-name url-shortener --name AspNetCoreFunction --tail
```

---

## Teardown

```bash
sam delete --stack-name url-shortener
```

SAM will prompt for confirmation before deleting the stack and its S3 artefacts.

> [!CAUTION]
> This permanently deletes the DynamoDB table and **all short URL records**. Back up any data you need before running this command.
