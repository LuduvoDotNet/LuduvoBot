# LuduvoBot

Discord bot that queries the Luduvo API and supports account verification via bio tokens.

## Requirements

- .NET SDK (target framework: net10.0)
- MariaDB or MySQL

## Configuration

Set the following environment variables:

- `DISCORD_TOKEN` (required)
- `LUDUVO_DB_CONNECTION` (optional full connection string)
- `LUDUVO_DB_HOST` (default: 192.168.1.17)
- `LUDUVO_DB_NAME` (default: luduvo)
- `LUDUVO_DB_USER` (default: luduvo)
- `LUDUVO_DB_PASSWORD` (required if no full connection string)
- `LUDUVO_DB_PORT` (default: 3306)
- `LUDUVO_VERIFY_TOKEN_TTL_MINUTES` (default: 15)

The database table is created automatically on first use.

## Verification commands

- `/verifystart username:<name>` starts verification and returns a token
- `/verifycheck username:<name>` checks the token in the bio
- `/verifystatus` shows current status
- `/verifyunlink` removes the linked account and pending verification
- `/verifyprofile` shows your linked Luduvo profile
- `/verifylookup member:<user>` shows a member's linked Luduvo profile (requires Manage Server)

## Build

Run from the repo root:

```bash
dotnet build
```
