# ServerPilot - Stage 1

Modern PWA for secure SSH access and Linux server management.

## Features (Stage 1)
- **Identity & Security**: JWT-based authentication with ASP.NET Core Identity.
- **Server Fleet**: Securely store and manage multiple Linux server credentials.
- **Interactive Shell**: Real-time terminal in the browser using SignalR and xterm.js.
- **PWA Support**: Installable on mobile and desktop for one-tap console access.
- **DevOps Aesthetic**: Clean, dark high-precision interface.

## Tech Stack
### Backend
- **ASP.NET Core 8 Web API**
- **Entity Framework Core** (SQLite)
- **SignalR** (WebSockets)
- **SSH.NET** (SSH connectivity)

### Frontend
- **React + Vite**
- **TailwindCSS** (Vanilla CSS components)
- **xterm.js** (Terminal emulation)
- **Lucide React** (Icons)

## Installation & Setup

### Requirements
- .NET 8 SDK
- Node.js (v18+)

### 1. Backend Setup
```bash
cd ServerPilot.Api
dotnet restore
dotnet build
dotnet run
```
The API will start at `http://localhost:5242`.

### 2. Frontend Setup
```bash
cd client
npm install
npm run dev
```
The frontend will start at `http://localhost:5173`.

## Deployment (Vite PWA)
To build the PWA assets:
```bash
npm run build
```

## Security Note
This is Stage 1. Ensure you change the JWT secret in `appsettings.json` before any public deployment.
All credentials are stored in the `serverpilot.db` SQLite database.
