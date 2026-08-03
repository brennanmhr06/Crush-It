# Crush It

A Windows match-3 candy game built with **.NET 10** and **WinForms**. Swap candies, chain combos, earn gold, unlock achievements, and progress through level cycles stored in **MongoDB**.

## Features

- **Match-3 gameplay** — swap adjacent candies to make matches of three or more
- **Level progression** — 40 levels per cycle across 4 rows, with increasing difficulty
- **Achievements** — unlock and claim rewards for milestones (score, combos, matches, and more)
- **User accounts** — sign up and log in with progress saved to MongoDB
- **Home dashboard** — view stats, gold, rank, and profile info
- **Tutorial** — guided onboarding for new players
- **Sound & music** — background music and match sound effects via NAudio
- **Mobile-friendly scaling** — touch support and responsive UI scaling
- **Anti-cheat client** — API client for server-side score validation and session checks (backend not included)

## Requirements

- **Windows** (WinForms app)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [MongoDB Atlas](https://www.mongodb.com/atlas) cluster (or local MongoDB instance)

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/brennanmhr06/Crush-It.git
cd Crush-It
```

### 2. Configure MongoDB

Copy the example config and add your connection details:

```powershell
Copy-Item appsettings.example.json appsettings.json
```

Edit `appsettings.json`:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb+srv://<username>:<password>@<cluster>.mongodb.net/<database>?retryWrites=true&w=majority",
    "DatabaseName": "CrushIt"
  }
}
```

Alternatively, create an `appsettings.local.json` with the same structure. Local overrides are loaded on top of `appsettings.json` and are also excluded from git.

> **Do not commit `appsettings.json` or any file containing real credentials.** Only `appsettings.example.json` belongs in the repo.

### 3. Build and run

```bash
dotnet build
dotnet run
```

Or open the project in **Visual Studio** or **VS Code** (with the C# Dev Kit) and press F5.

## Project Structure

```
Crush-It/
├── API/              # HTTP client, anti-cheat service, request/response models
├── Core/             # Input handling, sound, graphics, transitions, mobile scaling
├── Data/             # Game models, MongoDB config, user accounts, achievements
├── UI/               # WinForms screens (game, lobby, home, achievements, sign-up)
├── Sounds/           # Audio assets (copied to output on build)
├── Program.cs        # Application entry point
└── appsettings.example.json
```

## Configuration

| File | Purpose | Committed to git? |
|---|---|---|
| `appsettings.example.json` | Template with placeholder values | Yes |
| `appsettings.json` | Your local MongoDB connection string | No |
| `appsettings.local.json` | Optional local override | No |

The API client reads from `API/ApiConfiguration.cs` by default. Update `BaseUrl` and `ApiKey` when you deploy a backend server.

## Development

### VS Code

Launch configs and build tasks are in `.vscode/`. Use **Run > Start Debugging** or:

```bash
dotnet watch run
```

### Key dependencies

- [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver) — user data persistence
- [NAudio](https://www.nuget.org/packages/NAudio) — audio playback
- [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration) — JSON config loading

## API / Backend

The `API/` folder contains a client-side implementation for score validation, achievement verification, session checks, and gameplay pattern reporting. See [API/README.md](API/README.md) for details.

The backend server endpoints are **not included** in this repository. The game falls back to local MongoDB authentication when the API is unavailable.

## License

This project is licensed under the [Apache License 2.0](LICENSE).
