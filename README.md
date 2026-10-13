# EdCo Platform

> Modern, AI-powered eLearning and academic management platform built with .NET 10, React Native / Expo, and containerized cloud services.

---

## 📖 Overview

**EdCo** is an end-to-end educational technology platform designed to streamline course delivery, AI-assisted grading, student assessment, and administrative management. The solution combines a high-performance backend, an intuitive web administration portal, an interactive mobile student client, and background AI processing pipelines.

---

## 🏛️ Architecture & Projects

| Project | Tech Stack | Description |
|---|---|---|
| **[EdCo.API](file:///c:/Users/makuy/Repos/EdCo/EdCo.API)** | ASP.NET Core (.NET 10.0), EF Core, Redis, JWT | RESTful backend API handling authentication, student workflows, AI services, and webhook integrations. |
| **[EdCo.AdminPortal](file:///c:/Users/makuy/Repos/EdCo/EdCo.AdminPortal)** | ASP.NET Core MVC (.NET 10.0), Bootstrap 5 | Administrative dashboard for course creation, enrollment, analytics, asset management, and billing. |
| **[EdCo.Core](file:///c:/Users/makuy/Repos/EdCo/EdCo.Core)** | .NET 10.0 Class Library | Shared domain entities, repository contracts, EF Core DbContext, migrations, and shared business services. |
| **[EdCo.Tests](file:///c:/Users/makuy/Repos/EdCo/EdCo.Tests)** | xUnit, Moq | Comprehensive unit and integration test suite covering security, grading queues, and services. |
| **[edco-student-app](file:///c:/Users/makuy/Repos/EdCo/edco-student-app)** | React Native, Expo 54, React 19 | Cross-platform mobile application for students to access lessons, submit assignments, and view grades. |
| **Caddy Reverse Proxy** | Caddy 2 (Alpine) | Edge reverse proxy routing HTTPS traffic with automatic TLS termination to backend services. |

---

## 🛠️ Prerequisites

- **.NET SDK**: `10.0` or later
- **Node.js**: `v20.x` or later (for mobile app)
- **Docker & Docker Compose**: v2.20+
- **Database**: Microsoft SQL Server 2022+ (or Docker container)
- **Cache**: Redis 7+ (or Docker container)

---

## 🚀 Getting Started

### 1. Clone Repository & Setup Configuration

```bash
git clone https://github.com/your-org/EdCo.git
cd EdCo
```

Create your local `.env` file based on [.env.example](file:///c:/Users/makuy/Repos/EdCo/.env.example):

```bash
cp .env.example .env
```

Ensure the following variables are configured in `.env`:
- `DB_PASSWORD`: Strong password for the SQL Server `sa` account
- `JWT_SECRET`: Minimum 32-character key for signing JWT tokens
- Integration keys (Groq, Gemini, Bunny.net, Paynow) as applicable

---

### 2. Running with Docker Compose (Recommended)

To start the full platform stack (SQL Server, Redis, EdCo.API, EdCo.AdminPortal, and Caddy proxy):

```bash
# Build and start all services
docker compose up -d --build

# Inspect running container status
docker compose ps

# View service logs
docker compose logs -f edco-api
```

#### Default Container Endpoints:
- **API Health Check**: `http://localhost:5000/healthz`
- **Admin Portal**: `http://localhost:5001`
- **SQL Server**: `localhost:1433`
- **Redis Cache**: `localhost:6379`

---

### 3. Local Development (Without Docker)

#### Backend (.NET Solution)

```bash
# Restore dependencies
dotnet restore EdCo.sln

# Apply database migrations
dotnet ef database update --project EdCo.Core/EdCo.Core.csproj --startup-project EdCo.API/EdCo.API.csproj

# Run unit tests
dotnet test EdCo.sln

# Run API
cd EdCo.API
dotnet run

# Run Admin Portal (in separate terminal)
cd EdCo.AdminPortal
dotnet run
```

#### Mobile Client (React Native / Expo)

```bash
cd edco-student-app

# Install dependencies
npm install

# Copy mobile environment file
cp .env.example .env

# Start Expo development server
npm start
```

Press `a` for Android Emulator, `i` for iOS Simulator, or `w` for Web.

---

## 🧪 Testing & Quality Assurance

Run the test suite across all projects:

```bash
dotnet test EdCo.sln --configuration Release --verbosity normal
```

Run deployment pre-flight checks:

```powershell
.\scripts\preflight-check.ps1
```

---

## 🔒 Security & Deployment

- **No Secrets in Source Control**: Secrets, API keys, and environment overrides (`.env`, `appsettings.*.json`) are blocked in [.gitignore](file:///c:/Users/makuy/Repos/EdCo/.gitignore) and [.dockerignore](file:///c:/Users/makuy/Repos/EdCo/.dockerignore).
- **Non-Root Containers**: Production Dockerfiles run under the unprivileged `USER app`.
- **CI/CD Pipeline**: GitHub Actions workflow ([ci-cd.yml](file:///c:/Users/makuy/Repos/EdCo/.github/workflows/ci-cd.yml)) runs automated test suites, produces self-contained EF migration bundles, pushes signed container images to GitHub Container Registry (GHCR), and deploys cleanly to target hosts.

---

## 📄 License

Proprietary / All Rights Reserved. See [LICENSE](file:///c:/Users/makuy/Repos/EdCo/edco-student-app/LICENSE) for module-specific licensing details.
