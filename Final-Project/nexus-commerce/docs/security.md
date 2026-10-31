# Security notes

## Implemented

- Passwords hashed with ASP.NET Core `PasswordHasher<T>` (PBKDF2); uniform login failure message; password policy (8+ chars, letter + digit).
- JWT validation (issuer, audience, lifetime, signature, 30 s skew) at gateway and in every service; roles checked server-side; admin endpoints require `Admin`; owner checks return 404 (no existence leak).
- Secrets only via environment variables (`Jwt__Key`, SA password, AI key, SMTP); `.env` is git-ignored; compose refuses to start without required secrets; services refuse to start with a short JWT key.
- No card numbers/CVV are collected or stored anywhere; the payment provider is a clearly-labelled mock (`isMock`, notice text in API and UI).
- Input validation on every DTO, capped paging, safe image URL rules (relative or https only), 1000-char chat limit, history ≤ 10 items.
- Rate limiting: gateway global + stricter on `/api/auth`; assistant 20 msgs/min per user/IP.
- CORS: explicit origin allow-list only. Error responses never include stack traces.
- Order creation is idempotent (`Idempotency-Key`), prices come from the server, stock is reserved atomically-with-retry.
- AI: read-only whitelisted tools, strict argument validation, untrusted-data labelling, sanitised product text, grounded cards, price check, fallback on provider failure, honest mode reporting; API keys never reach the browser.

## Development administrator password reset

Set `ADMIN_PASSWORD` in the ignored local `.env` file and temporarily set `ADMIN_RESET_PASSWORD=true`. Run `docker compose up -d --build --no-deps --force-recreate identity` once to apply it, then set `ADMIN_RESET_PASSWORD=false` and run that command again. The reset is honored only when `ASPNETCORE_ENVIRONMENT=Development`; it hashes the configured password with ASP.NET Core `PasswordHasher<T>` and does not recreate the database or remove any records. Never commit `.env` or leave the reset switch enabled.

## Known limitations (be aware before using beyond local development)

- JWT stored in `sessionStorage`: an XSS bug could steal it. No refresh tokens, no server-side revocation (disable a user → their existing token works until expiry at the gateway/services, except login).
- No email verification, password reset, MFA, or account lockout (the gateway rate-limits login attempts per IP).
- Rate limits are per process and trust `X-Forwarded-For` for the assistant when called directly.
- Development containers publish SQL Server, RabbitMQ and every service port to the host; RabbitMQ/SQL use the passwords from `.env`. TLS is not configured (put a TLS-terminating proxy in front).
- Admin product images are URLs only (no upload / malware scanning pipeline).
- Prompt-injection defences reduce, but cannot eliminate, the risk of a model producing misleading text; the model has no write capabilities by design.
- **Future write tools** (add to cart / place order) must: run under the caller's own JWT through the existing authenticated endpoints, be unavailable to anonymous chat, require an explicit confirmation step in the UI (not model text), and be covered by authorization tests.
