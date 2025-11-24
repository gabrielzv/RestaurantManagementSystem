PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- Insert restaurant if not exists
INSERT INTO Restaurants (Name, Address, CreatedAt)
SELECT 'Sabor Original', NULL, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM Restaurants WHERE Name = 'Sabor Original');

-- Insert menu items (idempotent)
INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Ceviche de Corvina', 'Fresca corvina marinada en limón, con cebolla morada, cilantro y un toque de ají.', 4500, 'Starter', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Ceviche de Corvina' AND Price = 4500);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Tacos de Carnitas', 'Tortillas de maíz rellenas de cerdo desmenuzado y cocido, con cebolla y cilantro.', 5200, 'Starter', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Tacos de Carnitas' AND Price = 5200);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Nachos Supremos', 'Tortilla chips cubiertos con frijoles, carne molida, queso fundido, pico de gallo y guacamole.', 6500, 'Starter', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Nachos Supremos' AND Price = 6500);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Casado de la Casa', 'La tradición tica: arroz, frijoles, plátano maduro, ensalada, tortilla y su elección de carne, pollo o pescado.', 9500, 'Main Course', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Casado de la Casa' AND Price = 9500);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Pechuga a la Mostaza', 'Jugosa pechuga de pollo grillada, bañada en una salsa cremosa de mostaza y hierbas.', 11200, 'Main Course', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Pechuga a la Mostaza' AND Price = 11200);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Pasta Primavera', 'Fetuccini en salsa Alfredo con una mezcla de vegetales frescos salteados (brócoli, zanahoria, chayote y champiñones).', 10500, 'Main Course', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Pasta Primavera' AND Price = 10500);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Tres Leches', 'El clásico bizcocho esponjoso empapado en una mezcla de tres leches y coronado con merengue.', 3800, 'Dessert', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Tres Leches' AND Price = 3800);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Volcán de Chocolate', 'Un pequeño pastel de chocolate con un corazón caliente y fundido, servido con una bola de helado de vainilla.', 4500, 'Dessert', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Volcán de Chocolate' AND Price = 4500);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Flan de Coco', 'Delicado flan de huevo con un intenso sabor a coco y un caramelo suave.', 3500, 'Dessert', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Flan de Coco' AND Price = 3500);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Refresco de Cas', 'Refrescante bebida natural hecha a base de la fruta costarricense cas.', 2000, 'Beverage', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Refresco de Cas' AND Price = 2000);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Limonada Fresca', 'Limonada naturalmente refrescante, endulzada al gusto.', 2500, 'Beverage', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Limonada Fresca' AND Price = 2500);

INSERT INTO MenuItems (Name, Description, Price, Category, IsAvailable, CreatedAt)
SELECT 'Batido de Fresa', 'Espumoso batido natural de fresa con leche.', 3200, 'Beverage', 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM MenuItems WHERE Name = 'Batido de Fresa' AND Price = 3200);

-- Associate inserted menu items with 'Sabor Original' (idempotent)
INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Ceviche de Corvina'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Tacos de Carnitas'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Nachos Supremos'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Casado de la Casa'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Pechuga a la Mostaza'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Pasta Primavera'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Tres Leches'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Volcán de Chocolate'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Flan de Coco'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Refresco de Cas'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Limonada Fresca'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

INSERT INTO RestaurantMenuItems (RestaurantId, MenuItemId)
SELECT r.Id, m.Id
FROM Restaurants r, MenuItems m
WHERE r.Name = 'Sabor Original' AND m.Name = 'Batido de Fresa'
  AND NOT EXISTS (SELECT 1 FROM RestaurantMenuItems rm WHERE rm.RestaurantId = r.Id AND rm.MenuItemId = m.Id);

COMMIT;
