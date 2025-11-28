<!-- .github/copilot-instructions.md - guidance for AI coding agents working on Sushi Base -->
# Sushi Base — Copilot instructions

Short guidance to help AI coding agents be productive in this repository.

- **Project type:** Unity (2022.3 LTS) game + Node.js backend + Solidity contracts.
- **Top-level areas:** `Assets/` (Unity C# code & scenes), `backend/` (Node.js services), `blockchain/` (Solidity contracts and deployment).

What to prioritize
- Preserve Unity asset files and GUIDs: avoid changing binary files, `.meta` entries, or reserializing Unity scenes.
- Make small, local C# changes (single script/class) rather than sweeping style changes across `Assets/`.
- Keep backend changes limited to `backend/src` and follow `backend/package.json` scripts (`start`, `dev`, `test`). Node engine >=20.
- For contract work, follow `blockchain/README.md`: run `npm install`, set up `.env` from `env.example.txt`, then `npm run compile` and `npm test`.

Patterns & conventions discovered in repo
- Gameplay events are recorded client-side and sent to the backend for on-chain recording; see Unity: `Assets/Scripts/*` and the README references to `GameplayEventTracker.cs` / `SessionManager.cs`.
- Backend acts as the authorized signer for on-chain actions (transaction queue, nonce management). Look at `backend/src/services/transactionQueue.*` and `blockchain/` deployment scripts before changing flow.
- Move batching and gas-savings are important: moves are batched (e.g., 10 moves/tx). Respect batching when modifying event emission or backend queue logic.

Useful files to inspect before editing
- `backend/package.json` — run/test scripts and Node version requirement.
- `blockchain/README.md` — compile/test/deploy steps, `.env` requirements, contract names (`ChefNFT`, `GameCore`, `Leaderboard`).
- `Assets/` — Unity C# scripts; avoid changing scenes or prefabs unless implementing a feature or fixing a bug.
- `README.md` (repo root) — overall architecture, scoring formulas and expected behaviors (useful for tests and contract logic).

Testing & local workflows
- Backend: `cd backend && npm install && cp .env.example .env && npm run dev` for development (uses `nodemon`). Use `npm test` for backend tests.
- Contracts: `cd blockchain && npm install && cp env.example.txt .env && npm run compile && npm test`.
- Unity: open project in Unity Hub (2022.3 LTS), set `GameAPIClient` baseUrl to `http://localhost:3000` when running backend locally.

Agent behavior rules (specific)
- Do not modify Unity binary assets (`*.unity`, `*.prefab`) except when the change is strictly tied to the C# code you add and you verify in the Editor.
- When changing C# public API (method names, serialized fields), add a short README note in the same folder explaining the reason and backward-compatibility impact.
- When editing backend contract interaction code, update tests located under `blockchain/test` and `backend/test` where present.
- Prefer minimal diffs: split large tasks into focused PRs (e.g., "fix score calc bug in GameCore" vs "refactor event system").

Examples to reference in edits
- To adjust session scoring, inspect `blockchain/GameCore.sol` and tests in `blockchain/test` (see scoring formula in `blockchain/README.md`).
- To change event emission rate or batching, update Unity `GameplayEventTracker` and `backend/src/services/transactionQueue.*` together.
- To run the backend locally, inspect `backend/package.json` and `.env.example` for required env vars: `PRIVY_*`, RPC URLs, and backend signer keys.

If you are unsure
- Run the relevant local flow (backend dev server or contract tests) before proposing a PR.
- Ask the maintainer for Unity scene validation if your change touches scenes or prefabs.

Please review and tell me if you want more examples (specific filenames or a checklist for Unity changes).
