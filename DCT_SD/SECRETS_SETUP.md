# Local dev setup: required secrets

`dotnet run` needs several values that are deliberately kept out of source
control. This documents what's needed, why, and how to set it.

## Why you saw `RdFetchApi:Username is missing`

`WebApplication.CreateBuilder` only loads the `dotnet user-secrets` store
when the app's environment is **Development**. There's no `launchSettings.json`
in this project and `ASPNETCORE_ENVIRONMENT` isn't set, so `dotnet run` was
starting in **Production** — user secrets were silently skipped, even though
`RdFetchApi:Username` / `RdFetchApi:Password` were already set correctly.

Run with the environment variable set:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

(or `export ASPNETCORE_ENVIRONMENT=Development` once per shell / add it to
your shell profile.)

## Required secrets

All of these are read via `IConfiguration`, so they can come from either
`dotnet user-secrets` (recommended locally) or the equivalent
`Section__Key` environment variable.

| Config key | Where it's used | Status |
|---|---|---|
| `RdFetchApi:Username` | [ServiceCollectionExtensions.cs:47](Extensions/ServiceCollectionExtensions.cs#L47) — Basic Auth for the external RD Fetch API | ✅ already set in your user-secrets |
| `RdFetchApi:Password` | same | ✅ already set in your user-secrets |
| `Jwt:SigningKey` | [Program.cs:26](Program.cs#L26), [TokenService.cs:19](Services/TokenService.cs#L19) — HMAC-SHA256 signs the login JWT | ✅ generated and stored for you (see below) |
| `ConfigProtection:Key` | [ServiceCollectionExtensions.cs:101](Extensions/ServiceCollectionExtensions.cs#L101) — AES-256-GCM key that decrypts `ConnectionStrings:DefaultConnection` in `appsettings.json` | ❌ **still missing — see below** |

### `Jwt:SigningKey` — done

Any fresh random value works here (no dependency on existing data), so this
was generated and stored for you:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<32+ byte random base64 string>"
```

### `ConfigProtection:Key` — you need to get this from a teammate

This one is different: it's not a "pick any value" secret. The
`ConnectionStrings:DefaultConnection` value already committed in
`appsettings.json` is ciphertext (see
[ConfigProtector.cs](Helpers/ConfigProtector.cs)) — a base64-encoded
AES-256-GCM key was used to *encrypt* it, and the exact same key must be
supplied to *decrypt* it. Generating a new random key will not work; it'll
throw a cryptographic authentication failure on startup.

Git blame shows `ConfigProtector.cs` was added by `bindu.challa@paradigmit.com`
on 2026-08-25 (commit `a994a0b`, "User Management Changes and DB Schema
Change") — that's the most likely person/PR to check with, or check wherever
your team keeps shared secrets (password manager, CI secrets, etc.).

Once you have it:

```bash
dotnet user-secrets set "ConfigProtection:Key" "<the base64 AES-256 key>"
```

**If the original key is genuinely lost**, there's no way to recover the
existing connection string — the only path forward is to pick a new key,
re-encrypt the real DB connection string with `ConfigProtector.Encrypt(...)`,
replace `ConnectionStrings:DefaultConnection` in `appsettings.json`, and
distribute the new key to the team. That changes committed, shared config, so
don't do it unilaterally — loop in the team first.

## Quick checklist

```bash
# 1. Confirm what's already in your user-secrets store
dotnet user-secrets list

# 2. Set the one still missing (get the value from a teammate)
dotnet user-secrets set "ConfigProtection:Key" "<value>"

# 3. Run in Development so user-secrets actually get loaded
ASPNETCORE_ENVIRONMENT=Development dotnet run
```
