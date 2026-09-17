# Authentication API ![ASP.NET Core Web API](https://img.shields.io/badge/ASP.NET%20Core%20Web%20API-512BD4?style=flat&logo=.net&logoColor=white)

### Modern Authentication API built with ASP.NET Core

> Secure. Modular. Scalable.

The **Authentication API** provides authentication and authorization solution with **JWT**, **rotating refresh tokens**, **email confirmation**, **rate limiting**, and **account lockout**.

---

## Key Features

- **JWT Authentication & Role-based Authorization**
- **Rotating Refresh Tokens with Reuse Detection** (one-time use; replay revokes the whole token family)
- **Email Confirmation** (delivered via Mailpit in local/dev)
- **Resend Confirmation** (invalidates prior confirmation tokens)
- **Account Lockout after Repeated Failed Logins**
- **Rate Limiting Middleware (per IP)**
- **Clean Architecture Principles**
- **Dockerized Local Environment** (API, PostgreSQL, Mailpit)
- **k6 End-to-End Test Suite**
- **Swagger UI**

---

## Tech Stack
- **.NET 10**
- **Entity Framework Core 10**
- **PostgreSQL**
- **JWT**
- **Swagger UI**
- **k6**
- **Docker Compose**
---


## API Endpoints
| Endpoint | Method | Description |
|-----------|--------|-------------|
| `/api/authentication/register` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Register a new user and send confirmation email |
| `/api/authentication/resend-confirmation` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Resend confirmation email, invalidating prior tokens |
| `/api/authentication/confirm` | ![GET](https://img.shields.io/badge/GET-4CAF50?style=flat) | Confirm email via token link |
| `/api/authentication/login` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Login user, returns JWT and refresh token |
| `/api/authentication/refresh` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Rotate refresh token, returns new JWT |
| `/api/authentication/revoke` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Revoke a refresh token  |
| `/api/authentication/me` | ![GET](https://img.shields.io/badge/GET-4CAF50?style=flat) | Get the current authenticated user |

---

## ![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)
This project is licensed under the [MIT License](LICENSE).

---
