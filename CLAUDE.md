# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

RollRadar searches BJJ fight videos by technique and opens the YouTube player at the moment the
technique is mentioned in the (English) commentary. The user is Brazilian and writes in Portuguese: answer in
Portuguese, keep UI text in Portuguese, and code identifiers/comments in English (the Python scripts in
`samples/` are the exception: they use Portuguese names, follow that).

Environment: Windows, PowerShell. Node 24 / npm 11, .NET SDK 8, Python 3.11 (libraries in the user site, so
don't run Python with `-I`). Paths contain spaces (`jiujitsu finder`): always quote them.

## Commands

```bash
# Run (API on http://localhost:5214, Swagger at /swagger; web on http://localhost:4200, proxies /api to the API)
dotnet run --project backend/RollRadar.Api --launch-profile http
npm --prefix frontend start

# Tests
dotnet test                                                           # backend (xUnit)
dotnet test --filter "FullyQualifiedName~TechniqueCatalogTests"       # one class / test
npm --prefix frontend test -- --watch=false                           # frontend (Vitest via ng test)
cd frontend && npx ng test --watch=false --include src/app/videos/technique-match.spec.ts   # one file

# Build
dotnet build
npm --prefix frontend run build

# Captions straight into PostgreSQL (only on the user's machine; YouTube blocks cloud IPs)
dotnet run --project backend/RollRadar.Coletor -- coletar --max-consultas 30 --cookies-navegador firefox:rollradar
dotnet run --project backend/RollRadar.Coletor -- status | reprocessar | importar | verificar

# Legacy file pipeline (run inside samples/)
python baixar_transcricoes.py --status                                # queue progress, no network
python baixar_transcricoes.py --max-consultas 100 --pausa 15 --lote 20 --descanso 30   # one gentle session
python montar_tecnicas.py                                             # rebuild saida/ from the cache, offline
```

The API loads data once at startup: after `montar_tecnicas.py`, restart the API. A running API locks
`bin/…/RollRadar.Api.exe`, so stop it before `dotnet build`/`dotnet test`.

## Architecture

Data lives in PostgreSQL (EF Core, `backend/RollRadar.Api/Data`). **`backend/RollRadar.Coletor`** (console, the only
part that touches YouTube) ports the Python pipeline below onto the database: it lists the channels into `videos`,
runs the `yt-dlp` executable per fight (cookies + bgutil PO token, json3 caption to a temp folder deleted right away),
and saves `transcript_segments` plus the `technique_moments` found by `TechniqueMatcher` (port of
`montar_tecnicas.py`, same hits) in one transaction. Same queue, priority, pacing and option names as the scripts.
`reprocessar` rebuilds moments from stored captions keeping curated/voted ones; `importar` loads `samples/cache/`.
The technique catalog lives only in the database (`techniques`, `technique_aliases`): `catalogo exportar [csv]` /
`catalogo importar <csv>` round-trip it through a CSV for editing. Tests use a copy at
`backend/RollRadar.Api.Tests/Data/tecnicas-bjj.csv`. Athletes work the same way (`athletes` table, names reviewed by
hand): `atletas exportar [csv]` writes the catalog plus names read from fight titles (`AthleteNames`, no AI),
`atletas importar <csv>` replaces the table with the edited CSV; `AthleteCatalog` matches name/aliases as whole words
in titles. While the table is empty the API falls back to the names read from the titles.

The original pipeline, connected by files on disk:

1. **`samples/baixar_transcricoes.py`** (only part that touches YouTube): `yt-dlp` lists the channels
   (FloGrappling, IBJJF) into a work queue `cache/fila.json`; `youtube-transcript-api` downloads English
   captions per fight into `cache/transcricoes/<videoId>.json`; `cache/lutas.json` is the registry of processed
   fights. Queue items have a status (`pendente`, `legenda`, `sem_legenda`, `curta`, `pulada_evento`) and are
   processed by priority (channel order, then views, then duration). Sessions resume where they stopped.
2. **`samples/montar_tecnicas.py`** (offline): matches every technique of `samples/tecnicas-bjj.csv` (now exported from the database) against
   the cached captions and writes `samples/saida/<technique-id>.json` + `indice.json`. Format documented in
   `samples/saida/FORMATO.md` (format version 2).
3. **API (`backend/RollRadar.Api`, .NET 8) + web (`frontend`, Angular 22)**:
   - `TechniqueCatalog` (loaded from the database by `DbTechniqueCatalog`) resolves what the user types (Portuguese, English, alias,
     accents/case/hyphens ignored) to catalog techniques. `TechniqueFileLoader` reads `saida/*.json`; the
     **file name is the technique id**, and `Technique.ToId` must produce the same slug as `slug()` in the
     Python scripts (e.g. `D'Arce Choke` → `d-arce-choke`).
   - `VideoSearchService` filters moments by technique id, dedupes, and orders videos by number of moments.
     `MomentMerger` collapses mentions of the same technique within 20 s.
   - Endpoints: `GET /api/videos?technique=&athlete=` → `SearchResult` (recognized techniques, English search
     terms, videos with moments); `GET /api/videos/techniques` → catalog with video counts and aliases.
   - Security: the site's controllers (`Videos`, `Moments`, `Curation`) are `[AllowAnonymous]`; every other endpoint
     falls back to requiring a bearer token (`Auth/ApiTokenAuthenticationHandler`, hashes in `api_tokens`), so new
     endpoints are closed by default. `DataController` (`/api/v1/dados/...`: techniques, videos, transcripts,
     moments with curation and votes, keyset pages) is for external clients and needs the `leitura` scope.
     Rate limits: per token for the data API, per IP for the site search (`ApiAccess` in appsettings). Curation
     still uses `X-Curator-Key`. Tokens are created/revoked with the collector's `token` command.
     User accounts (`Accounts/`, `AuthController`): Google signs in (`Auth:Google:ClientId`/`ClientSecret` in
     user-secrets; without them the API runs and `/api/auth/login` answers 503), then the `rr_session` cookie
     (HttpOnly, Lax, 30 days sliding, 401 instead of redirect) carries the `users.id` claim. Account endpoints
     (`/api/eu`, logout, later diary and map) use the `"User"` policy; the cookie never opens the data API.
     Data Protection keys live in `data_protection_keys`, so cookies survive restarts.
   - Training diary (RR-013, `Training/`, `Data/DbTrainingStore`, `TrainingController`, policy `"User"`): tables
     `training_sessions` (client-generated uuid, so POST is idempotent), `session_techniques`, `partners` (unique per
     user by normalized name), `rolls` (timed, optional length), `roll_finishes` (any number per roll, `by_me`, optional
     technique and position; none = nobody submitted), `roll_steps` (estive/fui bem/sofri), `game_nodes` (a Posições/Guardas technique + side, created on first
     use; edges come with RR-014) and `focus_points`; every query filters by the cookie's user id and everything
     cascades from `users` (`DELETE /api/conta`; `GET /api/conta/exportar` dumps it all). Choices travel as the stored
     snake_case names (`SnakeCaseEnum`); `TrainingValidation` checks scales 1–5, finish/position categories.
     Frontend `src/app/training/`: one-screen form with a localStorage draft and an outbox that retries
     (`training-sync.service.ts`), history by week, detail; `/conta` in `src/app/account/`. "Rola" is masculine in UI text.
   - Frontend (`src/app/videos/`): standalone components, signals, zoneless, OnPush by default.
     `technique-picker` (autocomplete matching PT/EN/aliases client-side, `technique-match.ts`),
     `technique-browser` (category tabs), `video-search` (results, athlete filter client-side, search kept in
     the URL as `?tecnica=`), `video-modal` (YouTube embed starting 5 s before the mention). Theme tokens
     (blue / white / light red) live in `src/styles.scss`.

Config: `appsettings.json` → `Techniques:CatalogPath` and `Transcripts:OutputPath` (relative to the API
project). The API runs with `InvariantGlobalization`, so accent folding uses an explicit table in
`TextNormalizer` (string normalization does not strip accents there); the test project uses the same setting.

## Change control

Every change is a card in `MUDANCAS.md` (repo root) with a sequential code `RR-001`, `RR-002`… (one sequence for
all types). Types: Requisição (`feature/`), Problema (`fix/`), Incidente (`hotfix/`, the published site is broken),
Tarefa (`chore/`, technical work). Status: Aberta → Em andamento → Concluída (or Descartada); numbers are never reused.
Before starting new work, propose the card (next free number) and the branch name; the user creates branches himself.
Branch `feature/RR-013-diario-de-treino`, commit `RR-013: adiciona tabela de treinos`, PR `RR-013 · Diário de treino`.
Big changes get child cards ("Parte de: RR-0xx"). Ideas stay in the ideas list until the user decides to do them.

## Decisions already made (don't reopen them)

- Data comes only from the two scripts. Filmot exports were removed on purpose; don't bring them back.
- Focus on fights with **English** commentary (`--idiomas en`). The FloGrappling captions are auto-generated,
  so YouTube's "Subtitles/CC" filter / Data API `videoCaption` can't be used to pre-filter (they only see
  uploaded captions). IBJJF disabled captions on almost all its videos (`TranscriptsDisabled`).
- A snapshot of the data is fine; re-run the scripts when notable fights come out.
- Any technique mention that is not random commentator chatter is useful, defenses included.
- Curation will be **manual** (user marks results good/bad). **Never use AI/LLMs for curation or technique
  extraction** (the user doesn't want to spend tokens on it).
- YouTube IP-blocks caption downloads after bursts (~120 quick requests got a block lasting more than 10 h).
  Keep the scripts slow (pauses, batches, rests, wait-and-retry on block). Don't add proxies, IP rotation or
  other ways around blocks; suggest waiting and smaller sessions instead.

Next steps the user has mentioned: manual good/bad curation, then a database (PostgreSQL + EF Core).
