# Food Expiration Tracker - Startup Architecture Blueprint

## 1) High-Level Architecture

### Runtime Components
- Mobile App (Flutter/React Native): Inventory, scan, dashboard, recipe suggestions, notifications.
- API (ASP.NET Core): Auth, products, OCR ingestion, recipe engine, analytics endpoints.
- Worker (Hangfire/Queue Worker): Daily expiry scans, retries, scheduled cleanup.
- Data Layer (PostgreSQL): Normalized transactional store.
- Cache (Redis): Session/token blocklist, recipe match cache, dashboard aggregates.
- Object Storage (S3/Azure Blob): OCR image uploads.
- Notification Provider (FCM): Push delivery.

### Clean Architecture Dependency Flow
- Domain -> no external dependencies.
- Application -> depends only on Domain + interfaces.
- Infrastructure -> implements Application interfaces.
- API -> depends on Application + Infrastructure wiring.

Business rules live in `Application` services/use-cases. Domain holds core entities and invariants. Infrastructure is replaceable (EF Core, Hangfire, Tesseract, FCM) without changing business logic.

## 2) ER Diagram Explanation

### Core Relationships
- `User (1) -> (M) Product`
- `Category (1) -> (M) Product`
- `Product (1) -> (M) ProductBatch`
- `ProductBatch (1) -> (M) Notification`
- `Recipe (1) -> (M) RecipeIngredient`

### Why Product + Batch Are Separated
- Same product name can have multiple purchases and expiry dates.
- Batch-level notifications and consumption require independent records.
- Prevents duplicate product rows while preserving expiry granularity.

### Why Notification Is Tied to Batch
- Notifications are generated per expiry event (batch-specific).
- Enables idempotency key (`BatchId + NotificationType`) to avoid duplicate sends.
- Supports audit/retry history for each expiring item.

## 3) Folder Structure

```text
FoodExpirationTracker/
  FoodExpirationTracker.Domain/
    Common/
    Entities/
    Enums/
  FoodExpirationTracker.Application/
    Abstractions/
    DTOs/
    Services/
  FoodExpirationTracker.Infrastructure/
    Repositories/
    Security/
    Ocr/
    Notifications/
  FoodExpirationTracker.Api/
    Controllers/
    Middleware/
    Extensions/
  docs/
    ARCHITECTURE.md
  infra/
    schema.sql
```

## 4) Database Schema

Defined in `infra/schema.sql`.

### PK/FK and Index Strategy
- `users.email` unique index.
- `products (user_id, normalized_name)` unique composite index.
- `product_batches (product_id, expiry_date, status)` composite index.
- `notifications (product_batch_id, notification_type)` unique composite index.
- `recipes` and `recipe_ingredients(recipe_id, normalized_ingredient_name)` index.

Performance notes:
- Partition `notifications` monthly after high write growth.
- Use partial index on active batches: `status='Active' AND deleted_at_utc IS NULL`.
- Cursor pagination for large inventories and notification history.

## 5) Core Service Design

- `AuthService`: register/login, password hashing, JWT issue.
- `ProductService`: create/find product, add batch, consume quantity.
- `OcrIngestionService`: OCR parse -> product/batch upsert.
- `NotificationService`: daily expiry scan, idempotent send, logging.
- `RecipeService`: ingredient match + expiry weight scoring.
- `DashboardService`: aggregate metrics for home screen.

## 6) Example API Endpoints

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `GET /api/v1/products`
- `POST /api/v1/products`
- `POST /api/v1/products/consume`
- `POST /api/v1/ocr/scan`
- `GET /api/v1/recipes/suggestions`
- `GET /api/v1/dashboard`
- `POST /api/v1/notifications/run-daily-job`

## 7) Background Job Logic

Daily at 08:00 local user timezone:
1. Query active batches with expiry in 7 days or 1 day.
2. Check notification log for existing (`batch_id`,`type`).
3. Send push via FCM.
4. Write `notifications` row with `success/error`.
5. Retry transient failures with exponential backoff.

## 8) Recipe Matching Algorithm

For each recipe:
- `matchPercent = matchedIngredients / recipeIngredientCount * 100`
- `expiryPriority = sum(weight for matched items expiring <= 3 days)`
- `finalScore = matchPercent + expiryPriority`

Sort by `finalScore DESC`, then `matchPercent DESC`.

## 9) Scaling Strategy

- Stateless API pods behind load balancer.
- Separate worker autoscaling based on queue depth.
- Read-heavy dashboard via Redis cache (short TTL).
- DB connection pooling + tuned indexes.
- Batch jobs chunked by user ranges to avoid thundering herds.
- Rate limiting at API gateway and app middleware.

## 10) Security Best Practices

- JWT with short expiry + refresh tokens.
- Password hashing with BCrypt (cost factor >= 12) in production.
- Strict input validation and model constraints.
- Signed URL image upload + MIME/size checks.
- Tenant isolation: every query filtered by `user_id`.
- HTTPS only, HSTS, secure headers.
- Audit logs for auth, OCR corrections, destructive actions.

## 11) Deployment Plan

- Docker multi-stage builds for API and worker.
- Environments: Dev, Staging, Prod with separate DB/Redis/keys.
- CI: build, tests, SAST, container scan, migration dry-run.
- CD: blue-green or canary rollout.
- Monitoring: OpenTelemetry + Prometheus/Grafana + centralized logs.
- Backups: nightly full + WAL/binlog point-in-time recovery.

## 12) Phased Development Roadmap

### Phase 1 (MVP - 4 to 6 weeks)
- Auth, products, batch tracking, basic OCR parse, dashboard, push reminders.

### Phase 2 (Stability - 4 weeks)
- EF Core persistence, Hangfire jobs, retry policies, observability, load tests.

### Phase 3 (Growth - 6 weeks)
- Offline sync, barcode scanning, family inventory, recommendation tuning.

### Phase 4 (Monetization)
- Premium plans, advanced analytics, AI expiry prediction, admin console.
