# FreshTrack - Food Expiration Tracker

A full-stack mobile-first application that helps you track food expiration dates, reduce waste, and discover recipes based on what's in your pantry.

## Features

### Pantry Management
- Add food items by scanning product labels with your camera (OCR powered by Google Gemini Vision)
- Manual entry with product name, expiry date, quantity, and category
- Edit and delete items
- Filter by status: All, Expiring Soon, Expired, Safe
- Filter by category with dropdown selector
- Custom product images via camera or upload

### Smart Notifications
- Automatic expiry alerts at 7 days, 3 days, 1 day, and on expiry day
- WhatsApp-style in-app toast notifications with sound
- Browser push notifications (Web Notification API)
- Mobile push notifications (Firebase Cloud Messaging + Capacitor Local Notifications)
- Email notifications for expiring items
- 6 notification sounds to choose from (5 synthesized + 1 custom MP3)
- Silent hours configuration
- Click any notification to jump directly to that product in your pantry
- Clear all notifications with one tap

### AI-Powered Recipe Suggestions
- Get recipe ideas based on items in your pantry
- AI-generated recipes via Google Gemini with ingredient matching
- 30+ built-in recipe catalog across breakfast, lunch, dinner, and snacks
- Detailed recipe view with ingredients, instructions, and cook time

### Dashboard
- Overview of pantry stats: total items, expiring soon, expired
- Monthly usage and waste tracking
- Quick access to all features

### User Management
- Email registration with OTP verification
- Login/logout with JWT authentication
- Password reset via email (forgot password flow)
- Profile editing with photo upload
- Change password

### Internationalization (i18n)
- English, Hindi, and Telugu language support
- Full app translation including notifications

### Dark Mode
- System-aware dark/light theme toggle
- Persisted preference

## Tech Stack

### Frontend
- **React 18** with TypeScript
- **Vite** for bundling and dev server
- **Tailwind CSS** for styling
- **shadcn/ui** component library
- **TanStack React Query** for server state management
- **React Router** for navigation
- **Capacitor** for native mobile (Android/iOS)
- **Sonner** for toast notifications
- **Lucide React** for icons

### Backend
- **ASP.NET Core** (.NET 10) Web API
- **Entity Framework Core** with PostgreSQL
- **Clean Architecture** (Domain, Application, Infrastructure, API layers)
- **JWT Authentication** with custom middleware
- **Google Gemini Vision API** for OCR and recipe generation
- **Firebase Admin SDK** for push notifications
- **SMTP Email Service** for email notifications

### Mobile
- **Capacitor** for Android/iOS native wrapper
- **Local Notifications** for scheduled expiry alerts
- **Push Notifications** via Firebase Cloud Messaging
- **Camera** for barcode/label scanning

## Project Structure

```
FreshTrack/
├── backend/
│   ├── FoodExpirationTracker.Api/          # Controllers, middleware, program entry
│   ├── FoodExpirationTracker.Application/  # Services, DTOs, interfaces
│   ├── FoodExpirationTracker.Domain/       # Entities, enums, value objects
│   └── FoodExpirationTracker.Infrastructure/ # EF Core, repositories, external services
├── frontend/
│   ├── client/
│   │   ├── components/                     # UI components (auth, layout, ui)
│   │   ├── hooks/                          # Custom React hooks
│   │   ├── lib/                            # API client, utils, i18n, notification logic
│   │   └── pages/                          # Route pages
│   ├── android/                            # Capacitor Android project
│   ├── public/                             # Static assets
│   └── server/                             # SSR server (optional)
└── README.md
```

## Prerequisites

- **Node.js** >= 18
- **pnpm** (or npm/yarn)
- **.NET 10 SDK**
- **PostgreSQL** database
- **Android Studio** (for APK build)
- **Google Gemini API Key** (for OCR and recipes)
- **Firebase Service Account** (for push notifications)

## Environment Variables

### Backend (set on server or in environment)

| Variable | Description |
|----------|-------------|
| `DATABASE_URL` | PostgreSQL connection string |
| `JWT_SECRET_KEY` | Secret key for JWT token signing |
| `GEMINI_API_KEY` | Google Gemini API key for OCR and AI recipes |
| `SMTP_SENDER_EMAIL` | Email address for sending notifications |
| `SMTP_APP_PASSWORD` | App password for SMTP email |
| `SMTP_HOST` | SMTP server host (default: smtp.gmail.com) |
| `SMTP_PORT` | SMTP port (default: 587) |

### Frontend

| Variable | Description |
|----------|-------------|
| `VITE_API_BASE_URL` | Backend API URL (default: proxied to localhost:5001) |

## Setup & Running

### Backend

```bash
cd backend/FoodExpirationTracker.Api

# Set environment variables
export DATABASE_URL="Host=localhost;Database=foodtracker;Username=postgres;Password=yourpassword"
export JWT_SECRET_KEY="your-secret-key-at-least-32-chars"
export GEMINI_API_KEY="your-gemini-api-key"

# Run
dotnet run
```

The API starts on `http://localhost:5001`.

### Frontend

```bash
cd frontend

# Install dependencies
pnpm install

# Run dev server
pnpm dev
```

The app starts on `http://localhost:8080` with API proxy to backend.

### Build for Production

```bash
# Frontend
cd frontend && pnpm build

# Backend
cd backend && dotnet publish -c Release
```

### Build Android APK

```bash
cd frontend

# Build the web app
pnpm build

# Sync with Capacitor
npx cap sync android

# Open in Android Studio
npx cap open android

# Or build from command line
cd android && ./gradlew assembleDebug
```

The APK will be at `android/app/build/outputs/apk/debug/app-debug.apk`.

## API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/auth/register` | Send verification email |
| POST | `/api/v1/auth/verify` | Verify OTP and create account |
| POST | `/api/v1/auth/login` | Login with email/password |
| POST | `/api/v1/auth/logout` | Logout |
| POST | `/api/v1/auth/forgot-password` | Send password reset OTP |
| POST | `/api/v1/auth/reset-password` | Reset password with OTP |

### Products & Pantry
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/products` | Get all products with batches |
| POST | `/api/v1/products` | Add a new product |
| PUT | `/api/v1/products/batches/{batchId}` | Update a batch |
| DELETE | `/api/v1/products/batches/{batchId}` | Delete a batch |
| DELETE | `/api/v1/products/{productId}` | Delete a product |

### OCR (Scanner)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/ocr/scan-image` | Scan product image with Gemini Vision |
| POST | `/api/v1/ocr/scan-image-preview` | Preview scan results |

### Recipes
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/recipes/suggestions` | Get recipe suggestions based on pantry |

### Notifications
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/notifications/history` | Get notification history |
| POST | `/api/v1/notifications/test` | Send test notifications |
| POST | `/api/v1/notifications/run-daily-job` | Trigger expiry scan |
| DELETE | `/api/v1/notifications/clear` | Clear all notifications |
| POST | `/api/v1/notifications/register-device` | Register FCM device token |

### Profile
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/profile/me` | Get user profile |
| PUT | `/api/v1/profile` | Update profile |
| POST | `/api/v1/profile/change-password` | Change password |

### Dashboard
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/dashboard` | Get dashboard stats |

## Deployment

### Backend (Render)
1. Create a new Web Service on Render
2. Connect your GitHub repo
3. Set build command: `cd backend && dotnet publish -c Release -o out`
4. Set start command: `cd backend/out && dotnet FoodExpirationTracker.Api.dll`
5. Add environment variables (DATABASE_URL, JWT_SECRET_KEY, etc.)

### Frontend (Render Static Site or Vercel)
1. Set build command: `cd frontend && pnpm install && pnpm build`
2. Set publish directory: `frontend/dist/spa`
3. Add `VITE_API_BASE_URL` pointing to your backend URL

## License

MIT
