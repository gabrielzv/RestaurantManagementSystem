# Quick Start Guide

This guide will help you get the Restaurant Management System up and running in minutes.

## Prerequisites Check

Make sure you have these installed:
- .NET 9.0 SDK: Run `dotnet --version` (should show 9.x.x)
- Node.js 20+: Run `node --version` (should show 20.x or higher)

## 🚀 Quick Start (5 minutes)

### Step 1: Clone and Navigate

```bash
git clone https://github.com/gabrielzv/RestaurantManagementSystem.git
cd RestaurantManagementSystem
```

### Step 2: Start the Backend

```bash
cd src/RestaurantManagement.API
dotnet run
```

✅ The API will start on `http://localhost:5245`
✅ Swagger UI available at `http://localhost:5245/swagger`
✅ Database is created automatically!

### Step 3: Start the Frontend (in a new terminal)

```bash
cd client
npm install
npm run dev
```

✅ Frontend will start on `http://localhost:5173`

### Step 4: Open Your Browser

Navigate to:
- **Frontend App**: http://localhost:5173
- **Swagger API Docs**: http://localhost:5245/swagger

## 🎯 Test the Application

### Using Swagger UI

1. Open http://localhost:5245/swagger
2. Click on **POST /api/MenuItems**
3. Click **Try it out**
4. Enter this JSON:
```json
{
  "name": "Margherita Pizza",
  "description": "Classic pizza with tomato, mozzarella, and basil",
  "price": 14.99,
  "category": "Main Course",
  "isAvailable": true
}
```
5. Click **Execute**
6. Refresh the frontend to see your new menu item!

### Using the Frontend

1. Open http://localhost:5173
2. You'll see the menu items displayed in a grid
3. Add items via Swagger and refresh to see them appear

## 📁 Project Structure

```
RestaurantManagementSystem/
├── src/
│   ├── RestaurantManagement.Domain/       # Business entities
│   ├── RestaurantManagement.Application/  # Business logic
│   ├── RestaurantManagement.Infrastructure/# Data access
│   └── RestaurantManagement.API/          # Web API
├── client/                                # Vue.js frontend
└── README.md                              # Full documentation
```

## 🔧 Configuration

### Change Database Port or Type

Edit `src/RestaurantManagement.API/appsettings.json`:

**For SQLite (default - recommended for development):**
```json
{
  "UseSqlite": true,
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=restaurant.db"
  }
}
```

**For SQL Server:**
```json
{
  "UseSqlite": false,
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RestaurantDb;Trusted_Connection=true;"
  }
}
```

### Change Frontend API URL

Edit `client/.env`:
```
VITE_API_URL=http://localhost:5245/api
```

## 📝 Available API Endpoints

- `GET /api/menuitems` - Get all menu items
- `GET /api/menuitems/{id}` - Get a specific menu item
- `POST /api/menuitems` - Create a new menu item
- `PUT /api/menuitems/{id}` - Update a menu item
- `DELETE /api/menuitems/{id}` - Delete a menu item

## 🛠️ Common Commands

### Backend
```bash
# Build the solution
dotnet build

# Run the API
cd src/RestaurantManagement.API
dotnet run

# Run in watch mode (auto-reload on changes)
dotnet watch run
```

### Frontend
```bash
# Install dependencies
npm install

# Run development server
npm run dev

# Build for production
npm run build

# Type-check
npm run type-check

# Lint
npm run lint
```

## ❓ Troubleshooting

**Backend won't start?**
- Make sure port 5245 is not in use
- Check .NET version: `dotnet --version`
- Try: `dotnet clean` then `dotnet build`

**Frontend won't connect to API?**
- Verify API is running at http://localhost:5245
- Check `client/.env` has correct API URL
- Clear browser cache and reload

**Database errors?**
- Delete `restaurant.db` file and restart the API
- The database will be recreated automatically

## 📚 Next Steps

- Read the full [README.md](README.md) for detailed documentation
- Explore the Clean Architecture structure
- Add your own entities and controllers
- Customize the Vue components

## 💡 Tips

- Use **Swagger UI** for API testing - it's interactive!
- The database auto-migrates on startup - no manual steps needed
- SQLite database file is created in the API project directory
- Both backend and frontend have hot-reload enabled

Happy coding! 🎉
