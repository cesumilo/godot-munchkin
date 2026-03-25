# Godot Munchkin

A digital multiplayer adaptation of the card game Munchkin, built with Godot 4.6 and C#.

## 🎮 Game Overview

- **Players**: 3–6 players per game session
- **Rules**: Based on official Steve Jackson Games rules (base game only, no expansions)
- **Architecture**: Client-server with authoritative server model
- **Networking**: HTTP REST API + WebSocket for real-time gameplay
- **Technology**: Godot 4.6 with C# (.NET), using custom networking (not Godot MultiplayerAPI)

## 🎯 Current Status

**MVP Development Phase** - Implementing the first gameplay loop following the 7-step plan in `MVP_PLAN.md`.

### ✅ Completed Systems
- Authentication & JWT-based login
- Lobby management (HTTP API)
- WebSocket client with reconnection handling
- Complete card data system (168 card definitions)
- Game state machine with all Munchkin phases
- 3D equipment panel with drag-and-drop
- Card3D plugin integration for 3D card visuals

### 🚧 In Progress
- **Step 1**: MunchkinCard3D visual system
- **Step 2**: MockServer for client-side game logic simulation
- **Step 3**: Main GameBoard scene with turn flow

## 🏗️ Architecture

### Networking
- **HTTP**: Lobby creation/joining, authentication, matchmaking
- **WebSocket**: Real-time gameplay (turn progression, combat, card plays)
- **Authoritative server**: All game logic validated server-side
- **Message protocol**: JSON-based with typed messages (see `PROTOCOL.md`)

### Game Flow
1. Login → Lobby → Start Game
2. Turn phases: Open Door → Combat/Look for Trouble/Loot Room → Charity → End Turn
3. Combat system with force calculation, flee mechanics, and rewards

### Code Structure
```
res://
├── Scenes/          # Godot scenes
├── Scripts/         # C# game logic
│   ├── Networking/  # HTTP, WebSocket, message protocol
│   ├── GameState/   # State machine, player management
│   ├── Cards/       # Card data, factory, visuals
│   ├── Systems/     # Equipment, combat, economy logic
│   └── UI/          # Interface controllers
├── Resources/       # Card definitions, assets
└── addons/card_3d/  # 3D card visualization plugin
```

## 🛠️ Development Setup

### Prerequisites
- Godot 4.6+ with .NET 8 support
- Git
- CSharpier for code formatting

### Development Workflow
1. Always check `MVP_PLAN.md` for current implementation status
2. Follow test-first methodology from `AGENTS.md`
3. Run `dotnet csharpier format .` before committing
4. Verify compilation before logic testing

## 📚 Documentation

- **`AGENTS.md`** - System prompt with game rules, architecture, and development methodology
- **`MVP_PLAN.md`** - 7-step implementation plan for the first gameplay loop
- **`PROTOCOL.md`** - Complete WebSocket message protocol specification
- **`TESTING.md`** - Testing strategy and procedures

## 🎲 Game Rules Reference

The implementation follows the official Munchkin rules document (`AGENTS.md`) which includes:

- Turn state machine (§7)
- Combat algorithm (§8)
- Equipment slot rules (§9)
- Victory conditions (§2)
- Edge cases (§14)

## 🚀 Roadmap

### Short-term (MVP)
1. Complete 7-step implementation plan
2. First playable turn cycle with mock server
3. Basic combat system
4. Multi-turn bot auto-play

### Medium-term
1. Real server integration
2. Full combat interaction window
3. Negotiation and alliance system
4. Equipment drag-and-drop between collections

### Long-term
1. Polish UI/UX
2. Sound effects and animations
3. Spectator mode
4. Statistics and leaderboards

## 📄 License

[Apache-2.0](LICENSE) - See LICENSE file for details.

## 🤝 Contributing

Currently in active development. The project follows strict test-first methodology and game rule compliance as specified in `AGENTS.md`.

---

*Munchkin is a trademark of Steve Jackson Games. This project is a fan-made digital adaptation for educational purposes.*
