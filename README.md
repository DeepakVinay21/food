# Food Expiration Tracker Monorepo

This repository is split into two maintained folders:

- `backend/` -> ASP.NET Core API, OCR service integration, domain/application/infrastructure layers
- `frontend/` -> React + Vite mobile-first frontend (UI from `food-expiry-tracker-a2b`)

## Backend
Path: `/Users/vinay/Documents/FoodApp/FoodExpirationTracker/backend`

Run OCR service first (recommended):
```bash
cd /Users/vinay/Documents/FoodApp/FoodExpirationTracker/backend/ocr-service
docker compose up --build
```

Run:
```bash
cd /Users/vinay/Documents/FoodApp/FoodExpirationTracker/backend
export PADDLE_OCR_URL="http://127.0.0.1:8090/ocr/extract"
dotnet build FoodExpirationTracker.slnx --no-restore /m:1 /p:UseSharedCompilation=false
dotnet run --project FoodExpirationTracker.Api --no-build
```

## Frontend (React)
Path: `/Users/vinay/Documents/FoodApp/FoodExpirationTracker/frontend`

Run:
```bash
cd /Users/vinay/Documents/FoodApp/FoodExpirationTracker/frontend
corepack pnpm install
corepack pnpm dev
```

Frontend URL: `http://localhost:8080`

## Notes
- Vite proxies `/api/*` to backend at `http://127.0.0.1:5000`.
- Login/Register are enabled and required for pantry, scan, recipes, and profile routes.
