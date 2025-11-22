# Model Organization Documentation

## Overview

The Shittim-Server project uses a centralized `Models/` directory with feature-based subdirectories for organizing different types of models and data transfer objects (DTOs).

## Directory Structure

```
Models/
├── GM/                      # Game Master tool models
│   ├── BaseApiModel.cs      # Base API request model with AccountId
│   ├── Data.cs              # GM data models (Character/Weapon/Gear/Arena info)
│   └── InventoryModel.cs    # GM API request/response models
├── SDK/                     # SDK endpoint models  
│   ├── CountryV2Model.cs    # Country detection API models
│   ├── EnterToyModel.cs     # ToySDK initialization models (Service, Idfa, Offerwall, etc.)
│   ├── GetPromotionModel.cs # Promotion banner API models
│   ├── GTableInfaceModel.cs # Game table interface models
│   ├── GuestModel.cs        # IMS account (guest) authentication models
│   ├── SignInWithTicketToyModel.cs  # ToySDK ticket sign-in models
│   └── TermsModel.cs        # Terms of service API models
├── BAContext.cs             # Entity Framework DbContext
├── BAContextFactory.cs      # DbContext factory for migrations
├── ConnectionGroup.cs       # Multi-region connection configuration
├── EventContentScenarioHistory.cs   # Event scenario completion tracking
├── SchoolDungeonStageHistory.cs     # School dungeon progress tracking
├── User.cs                  # User account database model
└── WeekDungeonStageHistory.cs       # Weekly dungeon progress tracking

Note: Network protocol base classes (ServerResponsePacket, ErrorPacket, WebAPIException, BasePacket, etc.) 
are defined in Schale/MX/NetworkProtocol/, matching atrahasis structure where they're in Plana/MX/NetworkProtocol/
```

## Model Categories

### 1. GM (Game Master) Models

**Location**: `Models/GM/`

**Purpose**: Models used by GM tools for server management and testing

**Key Files**:
- `BaseApiModel.cs` - Base class for all GM API requests, provides `AccountId` property
- `Data.cs` - Complex data models for characters, weapons, equipment, arena teams
- `InventoryModel.cs` - Request/response models for inventory manipulation, raids, settings

**Usage**: Used by `Controllers/GM/` endpoints and `GameMasters/` services

### 2. SDK Models

**Location**: `Models/SDK/`

**Purpose**: Models for external SDK integrations (ToySDK, authentication, promotions)

**Key Files**:
- `EnterToyModel.cs` - ToySDK initialization with service configuration, IDFA settings, offerwall
- `SignInWithTicketToyModel.cs` - Ticket-based authentication for ToySDK
- `TermsModel.cs` - Terms of service retrieval and agreement
- `GetPromotionModel.cs` - Promotion banner system with segmentation
- `GuestModel.cs` - IMS account (guest) authentication
- `CountryV2Model.cs` - Country/region detection
- `GTableInfaceModel.cs` - Game table interface with audit info

**Usage**: Used by `Controllers/SDK/` endpoints (ToySDKController, PublicApiController, etc.)

### 3. NetworkModels (Located in Schale)

**Location**: `Schale/MX/NetworkProtocol/`

**Purpose**: Low-level network protocol abstractions and packet definitions

**Key Types** (defined in Schale, NOT in Shittim-Server/Models):
- `BasePacket` - Abstract base for all packets with SessionKey and Protocol
- `RequestPacket` - Abstract base for request packets with ClientUpTime, Hash, etc.
- `ResponsePacket` - Abstract base for response packets with ServerTimeTicks, notifications
- `ServerResponsePacket` - Server response wrapper with Protocol and Packet strings
- `ErrorPacket` - Error response packet extending ResponsePacket
- `WebAPIException` - Custom exception for web API errors with ErrorCode
- `WebAPIErrorCode` - Error code enum for API responses

**Usage**: Used throughout protocol handlers and core network code. Import via `using Schale.MX.NetworkProtocol;`

**Note**: These classes match the atrahasis structure where they're defined in `Plana/MX/NetworkProtocol/` (their data library)

### 4. Database Models

**Location**: `Models/` (root level)

**Purpose**: Entity Framework database models and DbContext

**Key Files**:
- `BAContext.cs` - Main Entity Framework DbContext with all DbSets
- `BAContextFactory.cs` - Design-time factory for EF migrations
- `User.cs` - User account model with authentication fields
- `*History.cs` - Various progress tracking models (events, dungeons)
- `ConnectionGroup.cs` - Multi-region server connection configuration

**Usage**: Used by all services that interact with the database

### 5. Import/Export Models

**Location**: `Utils/Loaddata.cs`

**Purpose**: Account import/export and data migration

**Key Types**:
- `ImportAccountAuthRequest`/`Response` - Account authentication import
- `ImportAccountLoginSyncRequest`/`Response` - Account login sync import
- `ImportItemListRequest`/`Response` - Item list import
- `ImportAccountDB` - Full account database representation
- `AccountBanByNexonDB` - Ban status model
- `AccountData` - Complete account data for migration

**Usage**: Used by account import/export commands and services

## Naming Conventions

### Request/Response Models

- **Request models**: End with `Request` suffix (e.g., `GetUserRequest`, `TermsRequest`)
- **Response models**: End with `Response` suffix (e.g., `GetPromotionResponse`, `TermsResponse`)
- **Result models**: End with `Result` suffix for nested result data (e.g., `PromotionResult`, `SignInTicketResult`)

### Data Models

- **Info models**: End with `Info` suffix for data transfer objects (e.g., `CharacterInfo`, `WeaponInfo`)
- **DB models**: Imported from `Schale.Data.GameModel` with `DBServer` suffix (e.g., `CharacterDBServer`)
- **History models**: End with `History` suffix for progress tracking (e.g., `EventContentScenarioHistory`)

### Configuration Models

- **Config models**: Descriptive names ending in `Config` (e.g., `ServerInfoConfig`, `IrcConfig`)
- **Settings models**: Descriptive names for server settings (e.g., `ConnectionGroup`)

## Best Practices

### 1. Model Placement

- **SDK-facing models** → `Models/SDK/`
- **GM tool models** → `Models/GM/`
- **Database entities** → `Models/` (root)
- **Network primitives** → `Models/NetworkModels/`
- **Import/export** → Keep in `Utils/Loaddata.cs` (follows atrahasis pattern)

### 2. Namespace Conventions

All models should use the `Shittim.Models` namespace with optional subdirectory:
- `Shittim.Models.GM`
- `Shittim.Models.SDK`
- `Shittim.Models.NetworkModels`

### 3. Serialization

- Use `System.Text.Json.Serialization` attributes for JSON serialization
- Apply `[JsonPropertyName]` attributes for proper camelCase/snake_case mapping
- Use nullable reference types (`?`) appropriately for optional fields

### 4. Documentation

- Add XML comments to complex models
- Document non-obvious property purposes
- Include usage examples for important DTOs

## Migration Notes

### Deprecated Locations

- `NetworkModels/Base.cs` (old location) → REMOVED: Use `Schale.MX.NetworkProtocol` instead
- `Models/ServerPacket.cs` → REMOVED: Use `Schale.MX.NetworkProtocol.ServerResponsePacket` directly
- `Models/NetworkModels/` → REMOVED: Network protocol classes are in Schale library
- `Core/NetworkProtocol/NetworkModels.cs` → REMOVED: Duplicate definitions removed
- `BlueArchiveAPI.*` namespaces → Use `Shittim.*` namespaces for new code (legacy code still uses BlueArchiveAPI)

### Future Improvements

1. Consider splitting large model files (e.g., `InventoryModel.cs`) if they exceed 500 lines
2. Add validation attributes to request models (`[Required]`, `[Range]`, etc.)
3. Consider using records for immutable DTOs where appropriate
4. Add factory methods for complex model creation (e.g., `ArenaTeamData.FromCharacterIds()`)

## References

- Atrahasis/Phrenapates reference: `atrahasis/atrahasis-global/Phrenapates/Models/`
- Current implementation: `Shittim-Server/Models/`
- Schale data library: `Schale/Data/GameModel/`
