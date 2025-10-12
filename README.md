# Authentication API ![ASP.NET Core Web API](https://img.shields.io/badge/ASP.NET%20Core%20Web%20API-512BD4?style=flat&logo=.net&logoColor=white)

### Modern Authentication API built with ASP.NET Core

> Secure. Modular. Scalable.

The **Authentication API** provides authentication and authorization solution with **JWT**, **refresh tokens**, **email confirmation**, **rate limiting**, and **account lockout**.

---


## Key Features

- **JWT Authentication & Role-based Authorization**  
- **Rotating Refresh Tokens with One Time Use**  
- **Email Confirmation**  
- **Account Lockout after Repeated Failed Logins**  
- **Rate Limiting Middleware (per IP)**  
- **Clean Architecture Principles**    
- **Integration Tests using xUnit**  
- **Swagger**  

---

## Tech Stack
- **.NET 8 / ASP.NET Core Web API**
- **Entity Framework Core 8**
- **SQL Server**
- **JWT**
- **Swagger UI**
- **xUnit**
---


## API Endpoints
| Endpoint | Method | Description |
|-----------|--------|-------------|
| `/api/authentication/register` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Register a new user and send confirmation email |
| `/api/authentication/confirm` | ![GET](https://img.shields.io/badge/GET-4CAF50?style=flat) | Confirm email via token link |
| `/api/authentication/login` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Login user, returns JWT and refresh token |
| `/api/authentication/refresh` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Request new JWT with valid refresh token |
| `/api/authentication/revoke` | ![POST](https://img.shields.io/badge/POST-FF9800?style=flat) | Revoke refresh token |

---

## ![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)
This project is licensed under the [MIT License](LICENSE).

---

## Author
**Clement Mathole**   
*Built with ❤️*
