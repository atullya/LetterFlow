# JWT Authentication Practice - ASP.NET Core

A simple, well-documented practice project for understanding JWT (JSON Web Token) authentication in ASP.NET Core.

## 🎯 What This Project Demonstrates

1. **JWT Setup** - Configuring JWT Bearer authentication
2. **Token Generation** - Creating JWT tokens with claims
3. **Token Validation** - Validating tokens on protected endpoints
4. **Claims Extraction** - Reading user info from JWT claims
5. **Authorization** - Using `[Authorize]` attribute to protect endpoints

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK

### Run the Project
```bash
cd JwtAuthPractice
dotnet run
```

The API will start at `https://localhost:5001`

## 📋 API Endpoints

### 1. Login (Public - No Token Required)

**POST** `/api/auth/login`

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"password123"}'
```

**Request Body:**
```json
{
  "username": "testuser",
  "password": "password123"
}
```

**Response:**
```json
{
  "message": "Login successful",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

---

### 2. Get Current User (Protected - Requires Token)

**GET** `/api/auth/me`

```bash
curl -X GET https://localhost:5001/api/auth/me \
  -H "Authorization: Bearer <YOUR_TOKEN_HERE>"
```

**Response:**
```json
{
  "userId": "1",
  "username": "testuser",
  "email": "testuser@example.com",
  "customClaim": "CustomValue"
}
```

---

### 3. Get Profile (Protected - Requires Token)

**GET** `/api/auth/profile`

```bash
curl -X GET https://localhost:5001/api/auth/profile \
  -H "Authorization: Bearer <YOUR_TOKEN_HERE>"
```

**Response:**
```json
{
  "message": "Hello, testuser! This is your profile.",
  "profile": {
    "username": "testuser",
    "email": "testuser@example.com",
    "registeredAt": "2025-05-09T10:30:00"
  }
}
```

---

### 4. Public Endpoint (No Token Required)

**GET** `/api/auth/public`

```bash
curl -X GET https://localhost:5001/api/auth/public
```

**Response:**
```json
{
  "message": "This is a public endpoint. No token required."
}
```

---

## 🔐 How JWT Authentication Works

### Flow Diagram

```
1. USER LOGIN
   Client sends username/password
        ↓
   Server validates credentials
        ↓
   Server generates JWT token
        ↓
   Server returns token to client

2. PROTECTED REQUEST
   Client sends token in Authorization header
        ↓
   Server validates token signature
        ↓
   Server validates token expiration
        ↓
   Server extracts claims from token
        ↓
   Server processes request with claims
```

### JWT Token Structure

A JWT token has 3 parts separated by dots:

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 . eyJzdWIiOiIxIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ . SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
├─ Header (Algorithm & Token Type)
├─ Payload (Claims/Data)
└─ Signature (Verification)
```

## 📝 Key Concepts

### Claims
User information embedded in the token:
- `NameIdentifier` - User ID
- `Name` - Username
- `Email` - Email address
- Custom claims - Any additional data

### Token Validation
When a request arrives:
1. Extract token from `Authorization: Bearer <token>`
2. Verify signature using secret key
3. Check expiration time
4. Extract claims if valid

### Stateless Authentication
- Server doesn't store tokens
- Token contains all necessary info
- Scales across multiple servers
- Mobile/SPA-friendly

## 🔧 Configuration

Edit `appsettings.json` to customize:

```json
"Jwt": {
  "Key": "your-secret-key-here",
  "Issuer": "your-issuer",
  "Audience": "your-audience",
  "ExpirationMinutes": 60
}
```

## 🛡️ Security Notes

⚠️ **For Production:**
- Store JWT key in environment variables or Azure Key Vault
- Use HTTPS always
- Implement refresh tokens
- Add password hashing (BCrypt)
- Validate credentials against database
- Implement token blacklist for logout
- Use shorter expiration times

## 📚 Testing with Postman

1. **POST** to `/api/auth/login` with credentials
2. Copy the `token` from response
3. **GET** `/api/auth/me`
4. Add header: `Authorization: Bearer <token>`
5. Send request

## 🧩 File Structure

```
JwtAuthPractice/
├── Program.cs              ← JWT configuration
├── Controllers/
│   └── AuthController.cs   ← API endpoints
├── appsettings.json        ← Configuration
└── README.md               ← This file
```

## 💡 Next Steps (Learning Path)

1. ✅ Understand basic JWT flow
2. 📝 Add refresh tokens for better security
3. 🗄️ Connect to a real database
4. 🔒 Implement password hashing
5. 👥 Add role-based authorization
6. 🚀 Deploy to production

## 🎓 What You Learned

- JWT token structure and claims
- Server-side token generation
- Token validation and expiration
- Protected vs public endpoints
- Stateless authentication
- Claims extraction in ASP.NET Core

## 📖 Resources

- [Microsoft JWT Docs](https://learn.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt)
- [JWT.io Token Debugger](https://jwt.io) - Decode tokens to see claims
- [OWASP JWT Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)

---

**Happy Learning! 🚀**
