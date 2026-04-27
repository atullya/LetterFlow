# LetterFlow Stories

A Medium-style blogging and publishing platform built with ASP.NET Core 10, PostgreSQL, and TinyMCE.

## Features

- Rich text story editor (TinyMCE)
- Author profiles with avatars
- Blog feed with topics, featured posts, and staff picks
- Cookie-based authentication with BCrypt password hashing
- Structured application logging (Serilog → JSON files + in-memory viewer at `/Logs`)
- Swagger API docs at `/swagger`

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 MVC |
| Database | PostgreSQL via EF Core + Npgsql |
| Auth | Cookie auth + BCrypt.Net |
| Logging | Serilog (file sink, JSON formatter) |
| Editor | TinyMCE 6 |
| CSS | Custom (Medium-inspired) |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL running locally

### Setup

1. Clone the repo
   ```bash
   git clone https://github.com/YOUR_USERNAME/YOUR_REPO.git
   cd YOUR_REPO
   ```

2. Copy the example config and fill in your values
   ```bash
   cp LetterTemplatePractice/appsettings.json LetterTemplatePractice/appsettings.Development.json
   ```
   Edit `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=letterflow;Username=YOUR_USER;Password=YOUR_PASSWORD"
     },
     "Seed": {
       "AdminUsername": "admin",
       "AdminEmail": "admin@example.com",
       "AdminPassword": "YourStrongPassword123!"
     }
   }
   ```

3. Apply migrations
   ```bash
   cd LetterTemplatePractice
   dotnet ef database update
   ```

4. Run
   ```bash
   dotnet run
   ```
   App starts at `https://localhost:7205`

## Project Structure

```
LetterTemplatePractice/   ← Main ASP.NET Core web app
Logging/                  ← Shared class library (Serilog logger)
```

## License

MIT
