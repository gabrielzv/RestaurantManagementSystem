PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- Create Restaurants table
CREATE TABLE IF NOT EXISTS Restaurants (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL UNIQUE,
  Address TEXT,
  CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Create join table to associate existing MenuItems with Restaurants
CREATE TABLE IF NOT EXISTS RestaurantMenuItems (
  RestaurantId INTEGER NOT NULL,
  MenuItemId INTEGER NOT NULL,
  PRIMARY KEY (RestaurantId, MenuItemId),
  FOREIGN KEY (RestaurantId) REFERENCES Restaurants(Id) ON DELETE CASCADE,
  FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id) ON DELETE CASCADE
);

-- Insert sample restaurants if they don't exist
INSERT INTO Restaurants (Name, Address)
SELECT 'Luigi''s Pizzeria', '123 Pizza St.'
WHERE NOT EXISTS (SELECT 1 FROM Restaurants WHERE Name = 'Luigi''s Pizzeria');

INSERT INTO Restaurants (Name, Address)
SELECT 'Bistro Verde', '45 Green Ave.'
WHERE NOT EXISTS (SELECT 1 FROM Restaurants WHERE Name = 'Bistro Verde');

-- Ensure sample menu items exist (idempotent)
INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Margherita Pizza', 'Classic pizza with tomato, mozzarella, and basil', 14.99, 'Main Course', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Margherita Pizza' AND Price = 14.99);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Caesar Salad', 'Romaine lettuce, croutons, parmesan and Caesar dressing', 9.5, 'Starter', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Caesar Salad' AND Price = 9.5);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Chocolate Cake', 'Rich chocolate cake with ganache', 6.75, 'Dessert', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Chocolate Cake' AND Price = 6.75);

-- Associate menu items to restaurants (by name) without duplicating associations
-- Luigi's Pizzeria -> Margherita Pizza
INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Luigi''s Pizzeria' AND m.Name = 'Margherita Pizza'
  AND NOT EXISTS (
    SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id
  );

-- Bistro Verde -> Caesar Salad
INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Bistro Verde' AND m.Name = 'Caesar Salad'
  AND NOT EXISTS (
    SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id
  );

-- Bistro Verde -> Chocolate Cake
INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Bistro Verde' AND m.Name = 'Chocolate Cake'
  AND NOT EXISTS (
    SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id
  );

COMMIT;

-- End of script
