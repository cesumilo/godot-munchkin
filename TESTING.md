# Testing the Munchkin Networking System

This document explains how to test the WebSocket networking system we've built.

## System Components

### 1. **PROTOCOL.md** - WebSocket Protocol Specification
- Complete message protocol definition
- Client → Server and Server → Client messages
- Data structures and examples
- Shared with server team for coordination

### 2. **Main Scene** (`Scenes/Main/Main.tscn`)
- Login screen with username/password
- Login button validation (enabled only when both fields filled)
- HTTP authentication to server
- JWT token storage
- Automatic transition to Lobby on successful login

### 3. **Lobby Scene** (`Scenes/Lobby/Lobby.tscn`)
- List available lobbies
- Create new lobby
- Join existing lobby
- Start game (host only)
- Connect to WebSocket for real-time gameplay

### 4. **Core Networking Components**

#### **WebSocketClient.cs** (`Scripts/Networking/`)
- WebSocket connection management
- Automatic reconnection
- Message queueing when disconnected
- Connection state events
- Error handling

#### **MessageProtocol.cs** (`Scripts/Networking/`)
- Type-safe message definitions
- Message builders for client messages
- Message parsers for server messages
- Enums for all message types and actions

#### **NetworkManager.cs** (`Scripts/Networking/`)
- Singleton autoload for global network access
- Convenience methods for common actions
- WebSocket client initialization

#### **Lobby.cs** (`Scripts/UI/`)
- HTTP API calls for lobby management
- UI event handling
- WebSocket event integration
- Game state transition

## Testing Procedure

### Step 1: Verify Compilation
```bash
dotnet build
```
✅ Should show "Build succeeded" with 0 warnings/errors

### Step 2: Run Godot Project
1. Open Godot 4.6+
2. Load the project
3. Click "Run" (F5)

### Step 3: Test Login Flow
1. **Initial State:**
   - Login button should be disabled (grayed out)
   - Error label hidden

2. **Partial Input:**
   - Type username only → Login button remains disabled
   - Type password only → Login button remains disabled

3. **Full Input:**
   - Type both username and password → Login button enables

4. **Login Attempt:**
   - Click Login button
   - Button should disable immediately (prevents double-click)
   - HTTP request sent to `http://90.28.104.14:1337/auth/login`
   - Check Godot output console for:
     - `Success! Data received:` (on success)
     - `JWT Token stored: ...` (token stored)
     - `Transitioning to lobby scene...`

5. **Error Handling:**
   - Invalid credentials → Error message shown, button re-enables
   - Server error → Error message shown, button re-enables
   - Typing after error → Error clears, button updates

### Step 4: Test Lobby Flow
1. **Lobby List:**
   - Automatically fetches lobbies on entry
   - Shows "Fetching lobbies..." status
   - Displays available lobbies in list

2. **Create Lobby:**
   - Click "Create Lobby"
   - Shows "Creating lobby..." status
   - On success: "Lobby created: [id]"
   - Auto-joins created lobby
   - Shows "You are the host" status

3. **Join Lobby:**
   - Select lobby from list
   - "Join Lobby" button enables
   - Click "Join Lobby"
   - Shows "Joining lobby [id]..."
   - Connects to WebSocket

4. **WebSocket Connection:**
   - Attempts to connect to `ws://90.28.104.14:1337/lobby/[id]/ws`
   - Shows "WebSocket connected successfully!" on success
   - Automatically sends JOIN_GAME message with player ID
   - Host sees "Start Game" button

### Step 5: Test WebSocket Messages
1. **Connection Events:**
   - Check Godot output for WebSocket messages
   - `[WebSocketClient] Connected successfully`
   - `[WebSocketClient] Message sent: ...`

2. **Send Test Message:**
   - The system automatically sends JOIN_GAME after connection
   - Message format:
     ```json
     {
       "type": "JOIN_GAME",
       "data": {
         "player_id": "username",
         "token": "jwt-token-here"
       }
     }
     ```

3. **Receive Messages:**
   - Server should respond with GAME_STATE message
   - Check Godot output for:
     - `[WebSocketClient] Received: ...`
     - `[WebSocketClient] Parsed message type: GAME_STATE`

## Expected Console Output

### Successful Flow:
```
[Main] Success! Data received: {"token":"eyJ..."}
[Main] JWT Token stored: eyJ... (first 20 chars)
[Main] Player ID set to: username
[Main] Transitioning to lobby scene...
[Lobby] Initialized, player ID: username
[Lobby] Status: Fetching lobbies...
[WebSocketClient] Connecting to ws://90.28.104.14:1337/lobby/[id]/ws
[WebSocketClient] Connection attempt started
[WebSocketClient] Connected successfully
[WebSocketClient] Message sent: {"type":"JOIN_GAME","data":{"player_id":"username","token":"eyJ..."}}
[Lobby] WebSocket connected successfully!
```

### Error Flow:
```
[Main] Invalid credentials
[Lobby] Connection Error: 0 (if server unreachable)
[WebSocketClient] Connection closed: Code=1006, Reason=
[WebSocketClient] Attempting to reconnect...
```

## Troubleshooting

### Common Issues:

1. **Server Not Responding:**
   - Check if server is running at `http://90.28.104.14:1337`
   - Check network connectivity
   - Verify server logs

2. **WebSocket Connection Failed:**
   - Check if WebSocket endpoint is enabled on server
   - Verify JWT token is valid
   - Check CORS settings on server

3. **Messages Not Received:**
   - Check WebSocket connection state
   - Verify message format matches PROTOCOL.md
   - Check server is sending expected messages

4. **UI Issues:**
   - Verify all UI nodes exist in scenes
   - Check signal connections in scene files
   - Verify script references are correct

## Next Steps After Testing

1. **Coordinate with Server Team:**
   - Share PROTOCOL.md for alignment
   - Test end-to-end message flow
   - Verify game state synchronization

2. **Implement Game Logic:**
   - Create card resource system
   - Implement player state
   - Build game state machine
   - Create game board UI

3. **Enhance Features:**
   - Card drag and drop
   - Combat system
   - Equipment management
   - Turn phase transitions

## Contact Points

- **Server API:** `http://90.28.104.14:1337`
- **WebSocket Endpoint:** `/lobby/{id}/ws`
- **Protocol Specification:** `PROTOCOL.md`
- **Game Rules:** `AGENTS.md`

## Notes

- The system uses optimistic UI updates
- Messages are queued when disconnected
- Automatic reconnection every 5 seconds
- JWT tokens are stored for session persistence
- Player ID is currently username (will parse JWT in future)