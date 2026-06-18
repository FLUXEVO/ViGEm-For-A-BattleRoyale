# 🎮 ViGEm Stick Drift UI V8.2

A Windows desktop application that creates a virtual Xbox 360 or PS4 controller using ViGEm and applies configurable right-stick drift, jitter, and custom movement patterns.

---

# ✨ Features

## 🎯 Virtual Controller Support

- Xbox 360 virtual controller
- PS4 virtual controller

## 🎮 Stick Drift Controls

- Adjustable downward drift strength
- Direction selector:
  - Left
  - Center
  - Right

## 🔄 Jitter & Movement Patterns

Built-in movement modes:

- Off
- Shake
- Circle
- Custom

All patterns remain centered around the current stick position for more natural movement.

---

# 🛠 Custom Pattern System

V8.2 introduces a refined custom pattern workflow.

## Pattern Editor

- Left-click to add points
- Right-click to remove points
- Automatic path re-centering before playback
- Visual graph editor

## Dedicated Patterns Folder

Custom patterns are now stored separately from profiles.

### Benefits

- Easier sharing
- Easier importing/exporting
- Reusable across multiple profiles
- Cleaner profile configuration

## Pattern / Jitter File Format

```text
x,y,delay
```

### Example

```text
0,-10,50
5,-12,50
-5,-12,50
0,-10,50
```

| Field | Description |
|---------|-------------|
| x | Horizontal stick offset |
| y | Vertical stick offset |
| delay | Delay in milliseconds before moving to the next point |

---

# ⌨ Keyboard-to-Controller Mapping

Map keyboard keys directly to controller actions.

### Supported Inputs

- A / B / X / Y
- Cross / Circle / Square / Triangle
- LB / RB
- LT / RT
- Back / Start
- LS / RS
- D-Pad Up / Down / Left / Right

## Turbo / Rapid Fire

Optional per-action turbo mode with configurable firing rate (Hz).

---

# ⚙ Additional Features

- Mouse wheel disengage toggle
- Multiple temporary disable keys
- Profile management
- Independent pattern importing
- Persistent configuration saving

### Profiles Save

- Drift settings
- Disable keys
- Keyboard bindings

> Custom patterns and jitter configurations are stored separately in the dedicated **Patterns** folder and can be reused across multiple profiles.

---

# 📋 Requirements

- Windows 10 / 11
- ViGEmBus installed
- .NET 8 SDK

---

# 🚀 Run Locally

```powershell
dotnet restore .\VigemStickDriftUi.sln
dotnet build .\VigemStickDriftUi.sln -c Release
dotnet run --project .\src\VigemStickDriftUi\VigemStickDriftUi.csproj
```

# 📦 Publish

```powershell
dotnet publish .\src\VigemStickDriftUi\VigemStickDriftUi.csproj -c Release -r win-x64 --self-contained false -o .\publish
```