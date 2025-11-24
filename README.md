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
- **SQL Server / SQLite** - Database (SQLite by default for easy setup)
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
- (Optional) [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) - SQLite is used by default

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

#### Database Configuration

The application is configured to use **SQLite** by default for easy setup and portability. The database file `restaurant.db` will be created automatically in the API project directory.

**To use SQLite (default):**

The default `appsettings.json` is already configured:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=restaurant.db"
  },
  "UseSqlite": true
}
```

**To use SQL Server instead:**

Edit `src/RestaurantManagement.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RestaurantManagementDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "UseSqlite": false
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

#### Database Migration

The database will be created and migrated automatically when you run the application. No manual migration steps are required!

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

## Database dump (SQLite) — create and restore

This explains how to share a sample copy of the SQLite database (schema + data) without committing the binary .db file. Use this only for non-sensitive example data. Prefer migrations + seed for reproducible setups.

1. Prepare

- Stop the API if it's running:

```powershell
# from repo root
cd src\RestaurantManagement.API
# stop any running `dotnet run`
```

- Verify sqlite3 is installed:

```powershell
sqlite3 --version
```

If not installed, download the SQLite command-line shell: https://www.sqlite.org/download.html

2. Create the dump folder (if missing)

```powershell
cd src\RestaurantManagement.API
mkdir ..\db
```

3. Export the dump (recommended: use a backup to avoid WAL issues)

```powershell
# create a consistent backup
sqlite3 restaurant.db ".backup backup.db"

# export SQL from the backup into the repo
sqlite3 backup.db ".dump" > ..\db\dump.sql

# remove the temporary backup
del backup.db
```

If the app is stopped and you prefer to export directly:

```powershell
sqlite3 restaurant.db ".dump" > ..\db\dump.sql
```

4. Add the dump to the repository (only if it's safe to share)

```powershell
git add src\db\dump.sql
git commit -m "Add sqlite dump for sample data"
git push
```

5. Restore the dump on another machine (after `git pull`)

```powershell
cd src\RestaurantManagement.API

# stop the API and remove any old DB files
del restaurant.db
del restaurant.db-wal
del restaurant.db-shm

# recreate the DB from the dump
sqlite3 restaurant.db < ..\db\dump.sql
```

6. Recommended alternative: migrations + seed
   For a controlled, reproducible setup, commit EF Core migrations and seed code. After pulling changes, run:

```powershell
dotnet ef database update --project src/RestaurantManagement.Infrastructure --startup-project src/RestaurantManagement.API
```

Quick tips

- Keep `*.db` in .gitignore to avoid committing binary DB files.
- Only commit dump.sql for non-sensitive sample data.
- Prefer migrations + seed for development environments.
