# RestaurantManagementSystem

Restaurant management system for restaurants who want to transition entirely to the digital stage while providing the best customer service they can.

## Architecture

This project is built using **Clean Architecture** principles with the following structure:

- **Domain Layer** (`RestaurantManagement.Domain`): Contains enterprise business rules and entities
- **Application Layer** (`RestaurantManagement.Application`): Contains application business rules and interfaces
- **Infrastructure Layer** (`RestaurantManagement.Infrastructure`): Contains external concerns like database access
- **API Layer** (`RestaurantManagement.API`): ASP.NET Core Web API with Swagger/OpenAPI support

## Tech Stack

### Backend
- **.NET 9.0** - Latest .NET framework
- **ASP.NET Core Web API** - RESTful API framework
- **Entity Framework Core** - ORM for database access
- **SQL Server** - Database
- **Swagger/OpenAPI** - API documentation and testing

### Frontend
- **Vue 3** - Progressive JavaScript framework
- **TypeScript** - Type-safe JavaScript
- **Vue Router** - Client-side routing
- **Pinia** - State management
- **Axios** - HTTP client
- **Vite** - Build tool and development server

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) or SQL Server LocalDB

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/gabrielzv/RestaurantManagementSystem.git
cd RestaurantManagementSystem
```

### 2. Setup Backend

#### Install Dependencies
```bash
dotnet restore
```

#### Update Database Connection String

Edit `src/RestaurantManagement.API/appsettings.json` and update the connection string if needed:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RestaurantManagementDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

For SQL Server on Windows, you can use:
```
Server=localhost;Database=RestaurantManagementDb;Trusted_Connection=true;MultipleActiveResultSets=true
```

For SQL Server with credentials:
```
Server=localhost;Database=RestaurantManagementDb;User Id=your_user;Password=your_password;MultipleActiveResultSets=true
```

#### Create Database Migration

```bash
cd src/RestaurantManagement.API
dotnet ef migrations add InitialCreate --project ../RestaurantManagement.Infrastructure
dotnet ef database update
```

#### Run Backend

```bash
# From the API project directory
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000/swagger`

### 3. Setup Frontend

#### Install Dependencies

```bash
cd client
npm install
```

#### Configure API URL

The frontend is configured to connect to `http://localhost:5000/api` by default. You can change this in `client/.env`:

```
VITE_API_URL=http://localhost:5000/api
```

#### Run Frontend

```bash
npm run dev
```

The Vue app will be available at `http://localhost:5173`

## Running the Full Application

1. **Start the Backend** (in one terminal):
   ```bash
   cd src/RestaurantManagement.API
   dotnet run
   ```

2. **Start the Frontend** (in another terminal):
   ```bash
   cd client
   npm run dev
   ```

3. **Access the Application**:
   - Frontend: http://localhost:5173
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger

## API Endpoints

### Menu Items

- `GET /api/menuitems` - Get all menu items
- `GET /api/menuitems/{id}` - Get menu item by ID
- `POST /api/menuitems` - Create new menu item
- `PUT /api/menuitems/{id}` - Update menu item
- `DELETE /api/menuitems/{id}` - Delete menu item

## Testing the API

You can test the API using:

1. **Swagger UI**: Navigate to `http://localhost:5000/swagger`
2. **curl**:
   ```bash
   # Get all menu items
   curl http://localhost:5000/api/menuitems
   
   # Create a menu item
   curl -X POST http://localhost:5000/api/menuitems \
     -H "Content-Type: application/json" \
     -d '{"name":"Burger","description":"Delicious burger","price":9.99,"category":"Main","isAvailable":true}'
   ```

## Project Structure

```
RestaurantManagementSystem/
├── src/
│   ├── RestaurantManagement.Domain/          # Domain entities and business logic
│   │   ├── Common/
│   │   │   └── BaseEntity.cs
│   │   └── Entities/
│   │       └── MenuItem.cs
│   ├── RestaurantManagement.Application/     # Application interfaces and logic
│   │   └── Interfaces/
│   │       └── IApplicationDbContext.cs
│   ├── RestaurantManagement.Infrastructure/  # Data access and external services
│   │   └── Data/
│   │       └── ApplicationDbContext.cs
│   └── RestaurantManagement.API/            # Web API controllers and configuration
│       ├── Controllers/
│       │   └── MenuItemsController.cs
│       ├── Program.cs
│       └── appsettings.json
├── client/                                   # Vue.js frontend
│   ├── src/
│   │   ├── services/
│   │   │   └── api.ts
│   │   ├── views/
│   │   │   ├── HomeView.vue
│   │   │   └── AboutView.vue
│   │   └── App.vue
│   └── package.json
└── RestaurantManagement.sln                 # Solution file
```

## Development

### Backend Development

```bash
# Build the solution
dotnet build

# Run tests (when added)
dotnet test

# Add a new migration
dotnet ef migrations add <MigrationName> --project src/RestaurantManagement.Infrastructure

# Update database
dotnet ef database update
```

### Frontend Development

```bash
# Install dependencies
npm install

# Run development server
npm run dev

# Build for production
npm run build

# Lint and fix files
npm run lint

# Type-check
npm run type-check
```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License.

