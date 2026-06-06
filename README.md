# Personal Finance Manager

A full-stack personal finance web application built with a vanilla HTML/CSS/JS frontend and an ASP.NET Core 8 REST API backed by PostgreSQL. Users can track income and expenses, manage a savings goal, generate financial reports, and switch display currency with live exchange-rate conversion.

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture Overview](#architecture-overview)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration Reference](#configuration-reference)
- [Database Schema](#database-schema)
- [API Reference](#api-reference)
- [Frontend Pages](#frontend-pages)
- [Authentication Flow](#authentication-flow)
- [Currency Conversion Flow](#currency-conversion-flow)
- [Development Workflows](#development-workflows)
- [Troubleshooting](#troubleshooting)

---

## Features

- **Secure authentication** — JWT-based login and registration; passwords hashed with BCrypt (work factor 12); tokens expire after 24 hours
- **Multi-user isolation** — every database query is scoped to the authenticated user; users cannot access each other's data
- **Transaction management** — create, read, update, and delete income and expense entries with category, date, description, and status
- **Server-side filtering** — search by keyword, filter by type/category/date range; the server does the work, not the browser
- **Savings tracker** — incrementally add to savings or set values directly; goal progress is calculated and returned in the same response
- **Dashboard** — single API call returns all aggregates: all-time totals, current-month totals, savings progress, KPI label, and the five most recent transactions
- **Date-range reports** — query any period for income, expenses, balance, and transaction count
- **Live currency conversion** — change your display currency (EUR, USD, LEK); the app fetches a live exchange rate and converts every transaction amount, savings total, and savings goal in one atomic database transaction
- **Automatic legacy cleanup** — on every page load, any old `localStorage` keys from the pre-API version are silently removed

---

## Tech Stack

### Backend

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET / ASP.NET Core | 8.0 |
| Language | C# | 12 |
| ORM | Entity Framework Core | 8.x |
| Database driver | Npgsql EF Core Provider | 8.x |
| Database | PostgreSQL | 14+ |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) | 8.x |
| Password hashing | BCrypt.Net-Next | 4.0.3 |

### Frontend

| Layer | Technology |
|---|---|
| Markup / Style | HTML5, CSS3, Bootstrap 5.3 |
| Language | Vanilla JavaScript (ES6+), jQuery 3.7 |
| Fonts | DM Sans (Google Fonts) |
| Exchange rates | RapidAPI — Currency Conversion and Exchange Rates |

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                           BROWSER                                │
│                                                                  │
│   HTML pages  ──▶  scripts/app.js  ──▶  $.ajax() HTTP calls     │
│   (static UI)      (shared module)      Authorization: Bearer …  │
└───────────────────────────┬──────────────────────────────────────┘
                            │  HTTP/JSON   localhost:5000
                            ▼
┌──────────────────────────────────────────────────────────────────┐
│                    ASP.NET Core 8 Web API                        │
│                         PfmApi/                                  │
│                                                                  │
│  ┌─────────────┐   ┌──────────────┐   ┌────────────────────┐   │
│  │ Controllers │──▶│ AppDbContext │──▶│   PostgreSQL DB     │   │
│  │ (routing)   │   │  (EF Core)   │   │     pfm_db         │   │
│  └─────────────┘   └──────────────┘   └────────────────────┘   │
│                                                                  │
│  Middleware stack (in order):                                    │
│  UseCors → UseAuthentication → UseAuthorization → MapControllers │
└──────────────────────────────────────────────────────────────────┘
                            │
                   ┌────────▼────────┐
                   │  RapidAPI       │
                   │  (exchange rate │
                   │   for currency  │
                   │   conversion)   │
                   └─────────────────┘
```

**Request lifecycle example — adding a transaction:**

1. User fills the form in `new-transaction.html` and clicks Save.
2. jQuery posts JSON to `http://localhost:5000/api/transactions` with `Authorization: Bearer <jwt>`.
3. `TransactionsController` validates the JWT, extracts the user ID from the `sub` claim, validates the request body, and inserts a new row into `transactions` scoped to that user ID.
4. The API responds `201 Created` with the saved object as JSON.
5. The browser redirects to `transactions.html`.

---

## Project Structure

```
pfm/                                 Root — frontend lives here
│
├── README.md                        This file
├── DOCS.md                          Extended reference (for teammates)
│
├── index.html                       Entry point; redirects to auth or dashboard
├── authentication.html              Login + register (two-panel layout)
├── profile-setup.html               One-time setup shown after first registration
├── dashboard.html                   Main overview page
├── transactions.html                Full transaction list with search/filter
├── new-transaction.html             Add a transaction
├── edit-transaction.html            Edit an existing transaction (?id=)
├── savings.html                     Manage savings amount and goal
├── profile.html                     Edit profile; change currency
├── reports.html                     Custom date-range financial report
├── overview.html                    Simple redirect shim → dashboard
│
├── scripts/
│   └── app.js                       Shared JS module (PFM namespace)
│
├── styles/
│   ├── style.css                    All custom CSS
│   └── profile_setup.html           Legacy location (superseded by root profile-setup.html)
│
└── PfmApi/                          Backend — entire API lives here
    │
    ├── PfmApi.csproj                Project file; NuGet package list
    ├── Program.cs                   App startup: services, middleware pipeline
    ├── appsettings.json             Connection string, JWT config
    ├── appsettings.Development.json Dev-only overrides
    │
    ├── Models/                      EF Core entity classes (mirror DB tables)
    │   ├── User.cs
    │   ├── Profile.cs
    │   └── Transaction.cs
    │
    ├── Data/
    │   └── AppDbContext.cs          EF Core context; column config, indexes
    │
    ├── DTOs/                        Request/response shapes (API contract)
    │   ├── Auth/
    │   │   ├── RegisterRequest.cs
    │   │   ├── LoginRequest.cs
    │   │   └── AuthResponse.cs
    │   ├── Profile/
    │   │   ├── ProfileResponse.cs
    │   │   ├── UpsertProfileRequest.cs
    │   │   └── ConvertCurrencyRequest.cs
    │   ├── Savings/
    │   │   ├── AddSavingsRequest.cs
    │   │   ├── EditSavingsRequest.cs
    │   │   └── SavingsResponse.cs
    │   ├── Transactions/
    │   │   ├── TransactionRequest.cs
    │   │   ├── TransactionResponse.cs
    │   │   └── ReportResponse.cs
    │   └── Dashboard/
    │       └── DashboardResponse.cs
    │
    ├── Controllers/
    │   ├── AuthController.cs        POST /api/auth/register, /login; GET /api/auth/me
    │   ├── ProfileController.cs     GET/PUT /api/profile; POST /api/profile/convert-currency
    │   ├── SavingsController.cs     PATCH /api/savings/add, /edit
    │   ├── TransactionsController.cs GET/POST/PUT/DELETE /api/transactions
    │   ├── DashboardController.cs   GET /api/dashboard
    │   └── ReportsController.cs     GET /api/reports/summary
    │
    ├── Helpers/
    │   └── JwtHelper.cs             Generates signed JWT tokens
    │
    └── Migrations/                  EF Core auto-generated migration files
        ├── 20260606005959_InitialCreate.cs
        ├── 20260606005959_InitialCreate.Designer.cs
        └── AppDbContextModelSnapshot.cs
```

---

## Prerequisites

| Tool | Minimum version | Check |
|---|---|---|
| .NET SDK | 8.x | `dotnet --version` |
| PostgreSQL | 14 | `psql --version` |
| EF Core CLI | Any | `dotnet ef --version` |
| Modern browser | Chrome / Edge / Firefox | — |

Install the EF Core CLI globally if you have not already:

```
dotnet tool install --global dotnet-ef
```

---

## Quick Start

### 1. Clone / download

Place the project so that the root contains both the HTML files and the `PfmApi/` folder.

### 2. Create the database

Open pgAdmin or a `psql` terminal and run:

```sql
CREATE USER pfm_user WITH PASSWORD 'pfm_pass';
CREATE DATABASE pfm_db OWNER pfm_user;
GRANT ALL PRIVILEGES ON DATABASE pfm_db TO pfm_user;
```

### 3. Start the API

```bash
cd PfmApi
dotnet restore
dotnet build            # must report: 0 Error(s)
dotnet ef database update
dotnet run
```

Expected output:
```
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
```

Leave this terminal open.

### 4. Open the app

Double-click `index.html` (or open it in a browser). You will be redirected to the login page.

Register an account, complete the one-time profile setup, and you are in.

---

## Configuration Reference

All configuration lives in `PfmApi/appsettings.json`.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=pfm_db;Username=pfm_user;Password=pfm_pass"
  },
  "Jwt": {
    "Secret":      "pfm-jwt-secret-key-change-this-2026-abcdef123456",
    "Issuer":      "PfmApi",
    "Audience":    "PfmFrontend",
    "ExpiryHours": "24"
  }
}
```

| Key | What it controls | Notes |
|---|---|---|
| `ConnectionStrings.DefaultConnection` | PostgreSQL connection | Adjust Host, Port, Username, Password to match your installation |
| `Jwt.Secret` | HMAC-SHA256 signing key | Must be at least 32 characters; change before deploying |
| `Jwt.Issuer` | Token issuer claim | Must match what the frontend expects (no change needed locally) |
| `Jwt.Audience` | Token audience claim | Must match what the frontend expects (no change needed locally) |
| `Jwt.ExpiryHours` | Hours until a token expires | Default 24; user must log in again after expiry |

**Changing the API port:** add `"Urls": "http://localhost:XXXX"` to `appsettings.json` and update `API_BASE` in `scripts/app.js` to match.

---

## Database Schema

Three tables. All column names are snake_case. All money columns use `NUMERIC(14,2)`. All timestamps use `TIMESTAMPTZ`.

### `users`

| Column | Type | Constraints |
|---|---|---|
| `id` | `SERIAL` | Primary key |
| `full_name` | `VARCHAR(120)` | Not null |
| `username` | `VARCHAR(20)` | Not null, unique |
| `email` | `VARCHAR(255)` | Not null, unique |
| `password_hash` | `TEXT` | Not null — BCrypt hash, never the raw password |
| `created_at` | `TIMESTAMPTZ` | Not null, default `NOW()` |

### `profiles`

| Column | Type | Constraints |
|---|---|---|
| `id` | `SERIAL` | Primary key |
| `user_id` | `INT` | Not null, unique, FK → `users.id` CASCADE DELETE |
| `phone` | `TEXT` | Not null, default `''` |
| `age` | `SMALLINT` | Nullable, check 1–120 |
| `occupation` | `TEXT` | Not null, default `''` |
| `currency` | `VARCHAR(3)` | Not null, default `'EUR'`; must be EUR, USD, or LEK |
| `savings_goal` | `NUMERIC(14,2)` | Not null, default `0`, check ≥ 0 |
| `total_savings` | `NUMERIC(14,2)` | Not null, default `0`, check ≥ 0 |
| `created_at` | `TIMESTAMPTZ` | Not null, default `NOW()` |

> `full_name` and `username` are **not** stored on the profile — they live on `users` and are joined when needed. This keeps a single source of truth.

### `transactions`

| Column | Type | Constraints |
|---|---|---|
| `id` | `BIGSERIAL` | Primary key |
| `user_id` | `INT` | Not null, FK → `users.id` CASCADE DELETE |
| `date` | `DATE` | Not null |
| `type` | `VARCHAR(7)` | Not null; `'Income'` or `'Expense'` |
| `category` | `VARCHAR(30)` | Not null; validated against the predefined list |
| `description` | `VARCHAR(255)` | Not null |
| `amount` | `NUMERIC(14,2)` | Not null, check > 0 |
| `status` | `VARCHAR(10)` | Not null, default `'Completed'`; one of Completed / Pending / Verified / Flagged |
| `created_at` | `TIMESTAMPTZ` | Not null, default `NOW()` |

**Indexes:**
- Unique on `users.email`, `users.username`
- Unique on `profiles.user_id`
- Composite `(user_id, date DESC)` on `transactions` for efficient date-range queries

**Relationships:**

```
users (1) ──── (0..1) profiles       One user has at most one profile row
users (1) ──── (0..*) transactions   One user has many transaction rows
```

---

## API Reference

**Base URL:** `http://localhost:5000/api`

**Authentication:** all protected endpoints require:
```
Authorization: Bearer <jwt-token>
```

---

### Auth

#### `POST /auth/register`

Creates a new account. No authentication required.

**Request body:**
```json
{
  "fullName":  "Jane Smith",
  "username":  "janesmith",
  "email":     "jane@example.com",
  "password":  "secret123"
}
```

**Validation rules:**
- `fullName` — required, max 120 characters
- `username` — required, 4–20 characters, letters / numbers / underscore only
- `email` — required, valid email format
- `password` — required, min 8 characters, must contain at least one letter and one digit

**Success — 201 Created:**
```json
{
  "token":           "eyJ...",
  "fullName":        "Jane Smith",
  "username":        "janesmith",
  "email":           "jane@example.com",
  "profileComplete": false
}
```

**Errors:**
- `400 Bad Request` — validation failure; `{ "error": "..." }`
- `409 Conflict` — email or username already taken; `{ "error": "..." }`

---

#### `POST /auth/login`

Authenticates a user and returns a fresh token. No authentication required.

**Request body:**
```json
{ "email": "jane@example.com", "password": "secret123" }
```

**Success — 200 OK:**
```json
{
  "token":           "eyJ...",
  "fullName":        "Jane Smith",
  "username":        "janesmith",
  "email":           "jane@example.com",
  "profileComplete": true
}
```

`profileComplete` is `true` when the profile row exists **and** the phone field is filled in. The frontend uses this boolean to decide the redirect destination: `true` → `dashboard.html`, `false` → `profile-setup.html`.

**Errors:**
- `400 Bad Request` — missing fields
- `401 Unauthorized` — email not found or password wrong

---

#### `GET /auth/me` 🔒

Returns the currently authenticated user's identity. Useful as the canonical source of user data on any page.

**Success — 200 OK:**
```json
{
  "id":              1,
  "fullName":        "Jane Smith",
  "username":        "janesmith",
  "email":           "jane@example.com",
  "createdAt":       "2026-01-15T10:00:00+00:00",
  "profileComplete": true
}
```

---

### Profile

#### `GET /profile` 🔒

Returns the authenticated user's profile.

**Success — 200 OK:**
```json
{
  "fullName":     "Jane Smith",
  "username":     "janesmith",
  "email":        "jane@example.com",
  "phone":        "0691234567",
  "age":          25,
  "occupation":   "Student",
  "currency":     "EUR",
  "savingsGoal":  1000.00,
  "totalSavings": 250.00,
  "createdAt":    "2026-01-15T10:00:00+00:00"
}
```

**Error — 404 Not Found:** profile row has not been created yet (user skipped profile-setup). The frontend responds by redirecting to `profile-setup.html`.

---

#### `PUT /profile` 🔒

Creates the profile row if it does not exist; updates it if it does (upsert). Also updates `users.full_name` and `users.username`.

**Request body:**
```json
{
  "fullName":     "Jane Smith",
  "username":     "janesmith",
  "phone":        "0691234567",
  "age":          25,
  "occupation":   "Student",
  "currency":     "EUR",
  "savingsGoal":  1000.00,
  "totalSavings": 250.00
}
```

**Success — 200 OK:** same shape as `GET /profile`

**Errors:**
- `400 Bad Request` — validation failure
- `409 Conflict` — username already taken by a different user

---

#### `POST /profile/convert-currency` 🔒

Converts all financial values for the user from one currency to another in a **single atomic database transaction**. Either all values are updated, or none are (no partial state is possible).

The frontend is responsible for fetching the live exchange rate (via RapidAPI) and sending it here.

**Request body:**
```json
{
  "fromCurrency": "EUR",
  "toCurrency":   "USD",
  "rate":         1.082341
}
```

**What changes in the database:**
- Every `transactions.amount` row belonging to this user is multiplied by `rate` and rounded to 2 decimal places
- `profiles.total_savings` is multiplied by `rate` and rounded to 2 decimal places
- `profiles.savings_goal` is multiplied by `rate` and rounded to 2 decimal places
- `profiles.currency` is set to `toCurrency`

**Success — 200 OK:** same shape as `GET /profile` with updated values

**Errors:**
- `400 Bad Request` — `fromCurrency` does not match the current profile currency, invalid currency codes, or `rate ≤ 0`
- `500 Internal Server Error` — database transaction failed; the error message confirms no data was changed

---

### Savings

#### `PATCH /savings/add` 🔒

Increments `total_savings` by the given amount. Does not accept a negative value.

**Request body:**
```json
{ "amount": 150.00 }
```

**Success — 200 OK:**
```json
{
  "totalSavings": 400.00,
  "savingsGoal":  1000.00,
  "progress":     40.00
}
```

`progress` is the percentage of the savings goal reached (0–100), computed server-side.

---

#### `PATCH /savings/edit` 🔒

Directly sets both savings values, replacing whatever is stored.

**Request body:**
```json
{ "totalSavings": 500.00, "savingsGoal": 2000.00 }
```

**Success — 200 OK:** same shape as `/savings/add`

---

### Transactions

#### `GET /transactions` 🔒

Returns the authenticated user's transactions. All query parameters are optional and can be combined.

| Query param | Type | Example | Effect |
|---|---|---|---|
| `search` | string | `?search=grocery` | Case-insensitive substring match on description and category (`ILike`) |
| `type` | string | `?type=Expense` | Filter by `Income` or `Expense` |
| `category` | string | `?category=Salary` | Filter by category name |
| `startDate` | date | `?startDate=2026-01-01` | Only transactions on or after this date |
| `endDate` | date | `?endDate=2026-01-31` | Only transactions on or before this date |

Results are ordered `date DESC, id DESC` (newest first).

**Success — 200 OK:**
```json
{
  "count": 2,
  "items": [
    {
      "id":          "42",
      "date":        "2026-01-20",
      "type":        "Income",
      "category":    "Salary",
      "description": "January part-time salary",
      "amount":      500.00,
      "status":      "Completed",
      "createdAt":   "2026-01-20T14:00:00+00:00"
    }
  ]
}
```

> `id` is a **string** in all transaction responses. `BIGSERIAL` values can exceed JavaScript's safe integer range, so returning them as strings prevents silent precision loss.

---

#### `POST /transactions` 🔒

Creates a new transaction.

**Request body:**
```json
{
  "date":        "2026-01-20",
  "type":        "Expense",
  "category":    "Groceries",
  "description": "Weekly supermarket run",
  "amount":      65.50,
  "status":      "Completed"
}
```

**Valid Income categories:** Allowance, Grants, Scholarships, Salary, Freelance, Gift, Investment, Other Income

**Valid Expense categories:** Groceries, Entertainment, Utilities, Tuition, Transport, Housing, Health, Dining, Other Expense

**Valid statuses:** Completed, Pending, Verified, Flagged

**Success — 201 Created:** the created transaction object

---

#### `GET /transactions/{id}` 🔒

Returns a single transaction by its ID.

**Success — 200 OK:** single transaction object

**Error — 404 Not Found:** no transaction with that ID belonging to this user

---

#### `PUT /transactions/{id}` 🔒

Replaces all fields of an existing transaction. Request body is the same shape as `POST /transactions`.

**Success — 200 OK:** updated transaction object

**Error — 404 Not Found:** no transaction with that ID belonging to this user

---

#### `DELETE /transactions/{id}` 🔒

Permanently deletes a transaction.

**Success — 204 No Content**

**Error — 404 Not Found:** no transaction with that ID belonging to this user

---

### Dashboard

#### `GET /dashboard` 🔒

Returns every piece of data the dashboard page needs in a single request, replacing what was previously seven separate synchronous localStorage calls.

**Success — 200 OK:**
```json
{
  "profile": {
    "fullName":     "Jane Smith",
    "currency":     "EUR",
    "totalSavings": 250.00,
    "savingsGoal":  1000.00
  },
  "totals": {
    "income":   1500.00,
    "expenses":  600.00,
    "savings":   250.00,
    "balance":  1150.00
  },
  "monthly": {
    "income":   500.00,
    "expenses": 200.00
  },
  "savingsProgress": 25.00,
  "kpi": {
    "value": 60.00,
    "label": "Strong"
  },
  "recentTransactions": [ /* up to 5 transaction objects, newest first */ ]
}
```

**KPI thresholds:**

| Savings rate | Label |
|---|---|
| ≥ 35% | Strong |
| ≥ 15% | Stable |
| ≥ 0% | Watch closely |
| < 0% | Needs attention |

`balance` = all-time income − all-time expenses + current savings total.

**Error — 404 Not Found:** profile row does not exist. The frontend redirects to `profile-setup.html`.

---

### Reports

#### `GET /reports/summary?startDate=&endDate=` 🔒

Returns aggregated totals for the specified date range.

**Query parameters (both required):**
- `startDate` — YYYY-MM-DD
- `endDate` — YYYY-MM-DD

**Success — 200 OK:**
```json
{
  "startDate": "2026-01-01",
  "endDate":   "2026-01-31",
  "count":     12,
  "income":    1200.00,
  "expenses":   540.00,
  "balance":    660.00
}
```

**Errors:**
- `400 Bad Request` — missing or invalid dates, or `startDate` is after `endDate`

---

## Frontend Pages

| File | Route trigger | API calls |
|---|---|---|
| `index.html` | Opened directly | None — decodes `pfmJwt` from localStorage and redirects |
| `authentication.html` | Redirect from index | `POST /auth/register`, `POST /auth/login` |
| `profile-setup.html` | Redirect after registration | `GET /profile` (pre-fill), `PUT /profile` (submit) |
| `dashboard.html` | After login | `GET /dashboard` |
| `transactions.html` | Nav link | `GET /profile` (currency), `GET /transactions` |
| `new-transaction.html` | Button in transactions | `POST /transactions` |
| `edit-transaction.html` | Edit button on a row | `GET /transactions/{id}`, `PUT /transactions/{id}` |
| `savings.html` | Nav link | `GET /profile`, `PATCH /savings/add`, `PATCH /savings/edit` |
| `profile.html` | Avatar dropdown | `GET /profile`, `PUT /profile`, `POST /profile/convert-currency` |
| `reports.html` | Nav link | `GET /profile` (currency), `GET /reports/summary` |

All pages (except index and authentication) call `PFM.requireLogin()` on load. This function synchronously reads and validates the JWT from `localStorage`; if the token is absent or expired the page redirects immediately to `authentication.html` — no API round-trip needed.

---

## Authentication Flow

```
Register
────────
Browser                          API                         Database
   │  POST /auth/register         │                               │
   │ ────────────────────────────▶│                               │
   │                              │  INSERT INTO users            │
   │                              │ ────────────────────────────▶ │
   │                              │  ◀──────────────────────────  │
   │                              │  Generate JWT                 │
   │  201 { token, profileComplete: false }                       │
   │ ◀────────────────────────────│                               │
   │  localStorage.pfmJwt = token │                               │
   │  redirect → profile-setup    │                               │


Login
─────
   │  POST /auth/login            │                               │
   │ ────────────────────────────▶│                               │
   │                              │  SELECT + BCrypt.Verify       │
   │                              │ ◀───────────────────────────▶ │
   │                              │  Generate JWT                 │
   │  200 { token, profileComplete }                              │
   │ ◀────────────────────────────│                               │
   │  localStorage.pfmJwt = token │                               │
   │  redirect → dashboard or profile-setup                       │


Authenticated request
─────────────────────
   │  GET /dashboard              │                               │
   │  Authorization: Bearer eyJ…  │                               │
   │ ────────────────────────────▶│                               │
   │                              │  Verify JWT signature         │
   │                              │  Extract user_id from "sub"   │
   │                              │  SELECT … WHERE user_id = ?   │
   │                              │ ────────────────────────────▶ │
   │                              │ ◀──────────────────────────── │
   │  200 { … }                   │                               │
   │ ◀────────────────────────────│                               │
```

**JWT payload:**
```json
{
  "sub":      "1",
  "email":    "jane@example.com",
  "username": "janesmith",
  "fullName": "Jane Smith",
  "iat":      1748000000,
  "exp":      1748086400,
  "iss":      "PfmApi",
  "aud":      "PfmFrontend"
}
```

The frontend reads `fullName`, `username`, and `email` directly from the JWT payload without an API call:
```javascript
var payload = JSON.parse(atob(token.split('.')[1]));
// payload.fullName, payload.username, payload.email available instantly
```

**Important implementation note:** `Program.cs` sets `options.MapInboundClaims = false` inside `.AddJwtBearer()`. This is required in .NET 8 because the default JWT handler would silently remap the `sub` claim to a long URI string, making `User.FindFirstValue("sub")` return `null` and breaking user ID extraction in all controllers.

---

## Currency Conversion Flow

Currency conversion happens in `profile.html` when the user changes their currency selector and saves.

```
profile.html                scripts/app.js              RapidAPI           PfmApi
     │                           │                          │                  │
     │  user selects USD          │                          │                  │
     │  clicks Save              │                          │                  │
     │                           │                          │                  │
     │  1. PUT /profile           │                          │                  │
     │     (currency = EUR)  ────────────────────────────────────────────────▶ │
     │     save form fields first │                          │                  │
     │     with OLD currency      │                          │                  │
     │ ◀────────────────────────────────────────────────────────────────────── │
     │                           │                          │                  │
     │  2. convertUserFinancialData('EUR', 'USD')            │                  │
     │ ────────────────────────▶ │                          │                  │
     │                           │  GET /timeseries         │                  │
     │                           │  base=EUR&symbols=USD ──▶│                  │
     │                           │ ◀────────────────────── │                  │
     │                           │  rate = 1.0823           │                  │
     │                           │                          │                  │
     │                           │  POST /profile/convert-currency             │
     │                           │  { fromCurrency:'EUR', toCurrency:'USD',    │
     │                           │    rate: 1.0823 }  ────────────────────────▶│
     │                           │                          │  BEGIN TRANSACTION│
     │                           │                          │  UPDATE transactions│
     │                           │                          │  UPDATE profiles  │
     │                           │                          │  COMMIT           │
     │                           │ ◀──────────────────────────────────────────  │
     │  3. update form with       │                          │                  │
     │     converted values      │                          │                  │
```

**If step 2 fails (RapidAPI down or quota exceeded):** the profile fields saved in step 1 are kept, but the currency is **not** changed (still EUR). A warning banner is shown. No financial data is in an inconsistent state.

**If step 3 fails (database error):** the database transaction is rolled back. The API returns `500` with a message confirming no data was changed. The frontend reverts the currency selector and shows a warning.

---

## Development Workflows

### Run the API

```bash
cd PfmApi
dotnet run
# Ctrl+C to stop
```

### Rebuild after C# changes

```bash
dotnet build
dotnet run
```

### Add a new database column

1. Edit the model in `PfmApi/Models/`
2. Configure the column in `PfmApi/Data/AppDbContext.cs`
3. Generate and apply the migration:

```bash
dotnet ef migrations add YourDescriptiveName
dotnet ef database update
```

### Roll back the last migration

```bash
dotnet ef migrations remove          # removes the last migration file
dotnet ef database update PreviousName   # downgrades the database
```

### Inspect the database directly

```bash
psql -U pfm_user -d pfm_db

-- list all users
SELECT id, username, email, created_at FROM users;

-- list all transactions for user 1
SELECT id, date, type, category, amount FROM transactions WHERE user_id = 1 ORDER BY date DESC;
```

### Change the API port

In `PfmApi/appsettings.json`:
```json
"Urls": "http://localhost:5001"
```

In `scripts/app.js`, line 4:
```javascript
var API_BASE = 'http://localhost:5001/api';
```

---

## Troubleshooting

### "That username is already taken" immediately after registering

**Cause:** The backend cannot extract the user ID from the JWT — it sees `0` and treats the uniqueness check as matching every user.

**Fix:** Ensure `options.MapInboundClaims = false` is present inside `.AddJwtBearer()` in `PfmApi/Program.cs`. This is already applied in this codebase. If you see this error after a code change, verify the line was not accidentally removed.

**Recovery:**
1. Stop `dotnet run` (Ctrl+C)
2. Restart: `dotnet run`
3. Open DevTools → Application → Local Storage → delete the `pfmJwt` key
4. Log in again with your registered credentials (the account was created successfully)

---

### Stuck in a redirect loop (dashboard → profile-setup → error → repeat)

**Cause:** The profile `GET` returns 404 (profile not yet set up), but the profile-setup form is also failing, so the user cannot proceed.

**Fix:**
1. Delete `pfmJwt` from Local Storage (DevTools → Application → Local Storage)
2. Restart the API
3. Log in — you will land on `profile-setup.html` with a clean state
4. Complete the form; it should now save successfully

---

### API returns `401 Unauthorized` on every request

**Causes:**
- Token expired (24-hour lifetime)
- Token missing from `localStorage`
- Wrong `Jwt.Secret` in `appsettings.json` (if you changed it and the browser still holds an old token)

**Fix:** log out (or delete `pfmJwt` from Local Storage) and log in again to get a fresh token.

---

### Cannot connect to the database

1. Confirm PostgreSQL is running (pgAdmin shows green, or `psql -U pfm_user -d pfm_db` connects)
2. Check `appsettings.json` — `Host`, `Port`, `Username`, and `Password` must match your PostgreSQL installation
3. Confirm `dotnet ef database update` was run at least once (tables may not exist)

---

### `dotnet` command not found

The .NET SDK is not installed or was installed after the current terminal session was opened. Close all terminals, install the SDK from the [.NET download page](https://dotnet.microsoft.com/download/dotnet/8.0), open a new terminal, and verify with `dotnet --version`.

---

### Currency conversion shows "API error"

The RapidAPI exchange-rate service is unavailable or the free-tier quota is exhausted. The profile fields (name, phone, occupation, etc.) saved in the first step are kept. The currency is **not** changed. No transaction amounts are altered. Wait and try again later.

---

### Old `localStorage` keys (pfmUsers, pfmTransactions, etc.) visible in DevTools

These are leftovers from the pre-API version of the app. They are automatically removed on every page load by the cleanup block in `scripts/app.js`. Reload any page and they will disappear.

---

## License

This project was built for educational purposes. No license file is included — all rights reserved by the project authors.
