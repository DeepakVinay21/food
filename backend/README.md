# Food Expiration Tracker Backend

Production-oriented ASP.NET Core backend for the Food Expiration Tracker app.

## Implemented stack
- Clean Architecture layers: Domain, Application, Infrastructure, API.
- OCR image endpoint using PaddleOCR microservice (primary) + Tesseract fallback.
- Inventory, dashboard, recipes, auth, notification job flow.

## Backend OCR capabilities
`POST /api/v1/ocr/scan-image` (multipart/form-data)
- Form fields: `image` (file), `quantity` (int)
- Extracts and returns:
  - `productName`
  - `manufacturingDate` (if found)
  - `expiryDate`
  - `daysLeftToExpire`
  - `categoryName`
  - `isConfidenceLow`
- Creates/updates inventory batch from extracted data.

## OCR service (recommended)
Path: `/Users/vinay/Documents/FoodApp/FoodExpirationTracker/backend/ocr-service`

Run:
```bash
cd /Users/vinay/Documents/FoodApp/FoodExpirationTracker/backend/ocr-service
docker compose up --build
```

Health check:
`http://127.0.0.1:8090/health`

The API uses this endpoint by default:
`http://127.0.0.1:8090/ocr/extract`

Override with env var:
`PADDLE_OCR_URL`

## Run backend
```bash
cd /Users/vinay/Documents/FoodApp/FoodExpirationTracker/backend
export PADDLE_OCR_URL="http://127.0.0.1:8090/ocr/extract"
dotnet build FoodExpirationTracker.slnx --no-restore /m:1 /p:UseSharedCompilation=false
dotnet run --project FoodExpirationTracker.Api --no-build
```

Backend URL: `http://127.0.0.1:5000`

## Frontend location
`/Users/vinay/Documents/FoodApp/FoodExpirationTracker/frontend`
# Food-Tracker
# Food-Tracker
