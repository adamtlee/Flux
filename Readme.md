# Flux Bank API

A modern, high-performance Bank Management API built with **.NET 10**. This project demonstrates a clean architecture approach by separating the web interface from the data persistence layer using Entity Framework Core.

---

## 🏗 Project Structure

The solution is divided into two main projects to ensure a clean separation of concerns and maintainability.

### 1. `Flux.Api` (Web Layer)
* **Role**: Handles HTTP requests, routing, and API documentation.
* **Key Components**:
    * `Controllers/`: Contains `BankController.cs` which manages CRUD logic.
    * `Program.cs`: Configures dependency injection, middleware pipeline, and **OpenAPI (Scalar)**.
* **Dependencies**: `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, `Flux.Data`.

### 2. `Flux.Data` (Data Layer)
* **Role**: Manages the database schema, data models, and persistence.
* **Key Components**:
    * `Models/`: Contains the `BankAccount.cs` domain model.
    * `BankDbContext.cs`: The Entity Framework context used to interact with the SQLite database.
* **Dependencies**: `Microsoft.EntityFrameworkCore.Sqlite`.



---

## 🚀 Getting Started

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* A terminal (bash, zsh, or PowerShell)

### Installation & Setup

1.  **Clone the repository**
    ```bash
    git clone [https://github.com/adamtlee/Flux.git](https://github.com/adamtlee/Flux.git)
    cd Flux
    ```

2.  **Restore Dependencies**
    ```bash
    dotnet restore
    ```

3.  **Run the API**
    Navigate to the API project folder and start the server:
    ```bash
    cd src/Flux.Api
    dotnet run
    ```
    *The API will typically start at `http://localhost:5271`.*

---

## 📖 API Documentation

This project uses **Scalar** for modern, interactive API documentation. Once the application is running, you can explore and test the endpoints visually:

* **Interactive UI**: `http://localhost:5271/scalar/v1`
* **Raw OpenAPI JSON**: `http://localhost:5271/openapi/v1.json`



---

## 🛠 CRUD Operations

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **GET** | `/api/bank` | Retrieve all bank accounts |
| **GET** | `/api/bank/{id}` | Retrieve a specific account by GUID |
| **POST** | `/api/bank` | Create a new bank account |
| **PUT** | `/api/bank/{id}` | Update an entire account record |
| **PATCH** | `/api/bank/{id}/update-balance` | Partially update only the account balance |
| **DELETE** | `/api/bank/{id}` | Remove an account from the system |

### Example Create Request (cURL)
```bash
curl -X POST http://localhost:5271/api/bank \
-H "Content-Type: application/json" \
-d '{
  "owner": "Adam Lee",
  "balance": 1000.00,
  "accountType": "Checking"
}'
```

---

## 📦 Database

This project uses **SQLite** for data persistence. The database file (`flux_bank.db`) is automatically created when you first run the application. Data is persisted across application restarts.

---

## 🔐 JWT Auth (Local API)

The API is secured with `Microsoft.AspNetCore.Authentication.JwtBearer`.

```bash
# Set credentials in env vars (no hardcoded secrets in commands)
export API_USERNAME="${API_USERNAME:?set API_USERNAME}"
export API_PASSWORD="${API_PASSWORD:?set API_PASSWORD}"
```

```bash
# 1) Register a user
curl -s -X POST http://localhost:5271/api/auth/register \
    -H 'Content-Type: application/json' \
    -d "{\"username\":\"$API_USERNAME\",\"password\":\"$API_PASSWORD\"}"
```

```bash
# 2) Login and extract token (macOS/Linux zsh/bash)
TOKEN=$(curl -s -X POST http://localhost:5271/api/auth/login \
    -H 'Content-Type: application/json' \
    -d "{\"username\":\"$API_USERNAME\",\"password\":\"$API_PASSWORD\"}" \
    | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')

echo "TOKEN length: ${#TOKEN}"
```

```bash
# 3) Call protected endpoint
curl -s http://localhost:5271/api/bankaccounts \
    -H "Authorization: Bearer $TOKEN"
```

```bash
# Optional: verify unauthorized request returns 401
curl -s -o /dev/null -w "%{http_code}" http://localhost:5271/api/bankaccounts
```