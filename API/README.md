# CrushIt API

This folder contains the API client and anti-cheat service for the CrushIt game.

## Structure

- **IApiClient.cs** - Interface defining all API operations
- **ApiClient.cs** - Implementation of the API client with HTTP calls
- **ApiConfiguration.cs** - Configuration settings for the API
- **ApiInitializer.cs** - Static initializer for the API system
- **AntiCheatService.cs** - Service for tracking and validating gameplay

## Models

- **Models/ScoreValidationRequest.cs** - Score validation request/response models
- **Models/AchievementVerificationRequest.cs** - Achievement verification models
- **Models/SessionValidationRequest.cs** - Session validation models
- **Models/GameplayPatternReport.cs** - Gameplay pattern analysis models

## Features

### Score Validation
- ✅ Validates scores against mathematical constraints
- ✅ Checks for impossible scoring patterns
- ✅ Server-side validation of playtime vs score
- ✅ Checksum generation for data integrity

### Achievement Verification
- ✅ Verifies achievements were legitimately earned
- ✅ Requires proof data for achievement unlocks
- ✅ Server-side validation of achievement requirements
- ✅ Game state hash verification

### Session Validation
- ✅ Validates session integrity
- ✅ Detects time manipulation
- ✅ Prevents session hijacking
- ✅ Device fingerprinting

### Gameplay Pattern Analysis
- ✅ Tracks move timing and patterns
- ✅ Detects bot-like behavior
- ✅ Identifies suspicious gameplay patterns
- ✅ Reports patterns to server for analysis
- ✅ Pattern scoring and risk assessment

## Usage

```csharp
// Initialize API
ApiInitializer.Initialize(ApiConfiguration.Default);

// Create anti-cheat service
var antiCheat = ApiInitializer.CreateAntiCheatService(userId, sessionId);
await antiCheat.InitializeSessionAsync();

// Track gameplay
antiCheat.StartLevelTracking(level);
antiCheat.RecordMove();
antiCheat.RecordMatch(combo);

// Validate score
bool isValid = await antiCheat.ValidateScoreAsync(level, score, moves, playTime);

// Verify achievement
bool isLegit = await antiCheat.VerifyAchievementAsync(achievementType, proofData);

// Report gameplay pattern
await antiCheat.ReportGameplayPatternAsync(level);
```

## Implementation Status

✅ **COMPLETED:**
- HTTP client implementation
- Score validation API calls
- Achievement verification API calls
- Session validation API calls
- Gameplay pattern reporting API calls
- Client-side score validation logic
- Client-side move timing analysis
- Rapid move detection
- Impossible move detection
- Pattern scoring algorithm
- Risk level determination
- Checksum generation
- Device fingerprinting
- Error handling and logging
- Fail-open design for better UX

## Next Steps

The client-side API implementation is complete. To make it fully functional, you need to:

1. **Build the backend API server** to handle the endpoints:
   - `/validate-score` - Score validation endpoint
   - `/verify-achievement` - Achievement verification endpoint
   - `/validate-session` - Session validation endpoint
   - `/report-pattern` - Gameplay pattern reporting endpoint
   - `/achievement-status/{userId}/{achievementType}` - Achievement status check
   - `/register` - User registration endpoint with server-side password hashing
   - `/login` - User login endpoint with account flagging

2. **Deploy the API server** to your chosen hosting service

3. **Update the API configuration** with your actual API base URL and API key

4. **Integrate with GameFrame** to call anti-cheat methods during gameplay

## User Authentication Integration

The API now includes user registration and login with anti-cheat features:

### Registration Flow
- **Server-side password hashing** - Passwords are hashed on the server, never stored locally
- **Device fingerprinting** - Captures machine name, OS version, processor count for anti-cheat
- **Risk assessment** - Server evaluates registration risk (LOW/MEDIUM/HIGH)
- **Manual review flagging** - Suspicious registrations flagged for review
- **Fail-open design** - Falls back to local MongoDB if API unavailable

### Login Flow
- **Server-side authentication** - Credentials validated on server
- **Account flagging** - Suspicious accounts blocked from login
- **Device consistency** - Tracks device fingerprints for suspicious activity
- **Tutorial status sync** - Server tracks tutorial completion state
- **Local caching** - MongoDB used as local cache for offline play

### Security Features
- **No local password storage** - API-authenticated users have empty password field in MongoDB
- **Device fingerprinting** - Machine name, OS version, processor count, architecture
- **IP address tracking** - Sent with all auth requests
- **Timestamp validation** - Client timestamps sent for time manipulation detection
- **User agent verification** - Game client identification