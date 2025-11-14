-- Run with: sqlite3 "src/RestaurantManagement.API/restaurant.db" < scripts/seed_menuitems.sql

PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- Insert Margherita Pizza if no menu item with the same name and price exists
INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Margherita Pizza', 'Classic pizza with tomato, mozzarella, and basil', 14.99, 'Main Course', 1, datetime('now')
WHERE NOT EXISTS (
  SELECT 1 FROM MenuItems WHERE Name = 'Margherita Pizza' AND Price = 14.99
);

-- Insert Caesar Salad
INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Caesar Salad', 'Romaine lettuce, croutons, parmesan and Caesar dressing', 9.5, 'Starter', 1, datetime('now')
WHERE NOT EXISTS (
  SELECT 1 FROM MenuItems WHERE Name = 'Caesar Salad' AND Price = 9.5
);

-- Insert Chocolate Cake
INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Chocolate Cake', 'Rich chocolate cake with ganache', 6.75, 'Dessert', 1, datetime('now')
WHERE NOT EXISTS (
  SELECT 1 FROM MenuItems WHERE Name = 'Chocolate Cake' AND Price = 6.75
);

COMMIT;

-- Optional: you can add more INSERT ... WHERE NOT EXISTS blocks below
