# Zootact

Zootact is a real-time Dou Shou Qi web app with a cute UI, Firebase auth, SignalR multiplayer, PostgreSQL persistence, Redis live state, and a Python AI service for Smart Replay and anti-cheat analysis.

## Stack

- `backend/`: ASP.NET Core 8 API, SignalR hub, EF Core, Redis-backed live game state
- `frontend/`: React + Vite + TypeScript + Zustand + i18n
- `ai-service/`: FastAPI analysis and anti-cheat service

## Current Architecture

- Firebase is the only authentication system in v1.
- PostgreSQL stores users, matches, moves, stats, and match analysis.
- Redis stores active matches, active-player mappings, and disconnect timers.
- SignalR path is `/game-hub`.
- The frontend restores active matches via `GET /api/match/active` after auth bootstrap.

## Definition Of Done

- Firebase login succeeds and `POST /api/auth/sync` returns the canonical `UserDto`
- Authenticated users can join matchmaking from the home page
- A matched player is routed into a live game and joins SignalR on `/game-hub`
- Refresh during a game restores board, timers, and player identity from `GET /api/match/active`
- Resignation, timeout, draw agreement, normal wins, and disconnect forfeits all finalize one persisted match result
- Completed matches update Forest Points and user stats in PostgreSQL
- Smart Replay and anti-cheat summaries are available from `GET /api/match/{matchId}/analysis`
- `bun run build` passes for the frontend
- `pytest` passes for `ai-service`

## Local Setup

See [docs/COMPLETE_SETUP.md](D:/Anh-Quan/Codes/Zootact/docs/COMPLETE_SETUP.md) for the full bring-up steps and required Firebase configuration.

Tracked config files are kept safe for GitHub. Put machine-specific values in ignored local files such as `backend/Zootact.API/appsettings.Development.json` or the root `.env`, or inject them as environment variables in your host/deployment platform. The committed `.env.example` documents the local Docker defaults without exposing your real file.

## Deployment

For the most production-like zero-cost deployment, use the runbook in [DEPLOY_OCI_CLOUDFLARE.md](D:/Anh-Quan/Codes/Zootact/DEPLOY_OCI_CLOUDFLARE.md).

- Frontend: Cloudflare Pages
- Backend API + SignalR: OCI Always Free Ampere A1 VM
- PostgreSQL: self-hosted on the OCI VM
- Redis: self-hosted on the OCI VM
- AI service: disabled in the initial production rollout

The older split-PaaS free-tier option is documented in [docs/DEPLOY_FREE_TIER.md](D:/Anh-Quan/Codes/Zootact/docs/DEPLOY_FREE_TIER.md), but it is less production-like because the backend/database providers may sleep, expire, or rely on limited trial credits.

The frontend is production-ready for split-origin deployment through:

- `VITE_API_BASE_URL`
- `VITE_SIGNALR_URL`
- `VITE_PUBLIC_APP_URL`
- `VITE_ENABLE_AI_ANALYSIS`

The backend values that should come from environment variables before pushing or deploying are:

- `ConnectionStrings__PostgreSQL`
- `ConnectionStrings__Redis`
- `Frontend__AllowedOrigins` (preferred) or `Frontend__Url`
- `Firebase__ServiceAccountJson`, `Firebase__ServiceAccountJsonBase64`, or `Firebase__ServiceAccountKeyPath`
- `AiService__Enabled`
- `AiService__BaseUrl` when AI analysis is enabled

## Recommended Dev Workflow

Use the root scripts so Docker infra, backend, frontend, and optional AI service are started consistently:

- `bun run dev:infra`
- `bun run dev:backend`
- `bun run dev:frontend`
- `bun run dev:ai`
- `bun run dev:status`
- `bun run dev:down`

Docker is required for PostgreSQL and Redis. The backend and frontend still run as local processes in development.
The local PostgreSQL container publishes to host port `5433` by default to avoid conflicts with any PostgreSQL installed directly on Windows. The database still listens on `5432` inside Docker.
