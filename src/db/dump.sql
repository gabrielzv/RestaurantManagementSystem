PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "__EFMigrationsLock" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
    "Timestamp" TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);
INSERT INTO __EFMigrationsHistory VALUES('20251029222132_InitialCreate','9.0.10');
INSERT INTO __EFMigrationsHistory VALUES('20251029222426_InitialSqlite','9.0.10');
CREATE TABLE IF NOT EXISTS "MenuItems" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MenuItems" PRIMARY KEY AUTOINCREMENT,
    "Category" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "IsAvailable" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Price" decimal(18,2) NOT NULL,
    "UpdatedAt" TEXT NULL
);
INSERT INTO MenuItems VALUES(1,'Entradas','2025-11-16 16:10:30.0059817','Fresca corvina marinada en limón, con cebolla morada, cilantro y un toque de ají.',1,'Ceviche de Corvina',4500,NULL);
INSERT INTO MenuItems VALUES(2,'Entradas','2025-11-16 16:11:22.9585745','Tortillas de maíz rellenas de cerdo desmenuzado y cocido, con cebolla y cilantro.',1,'Tacos de Carnitas',5200,NULL);
INSERT INTO MenuItems VALUES(3,'Entradas','2025-11-16 16:12:03.7675173','Tortilla chips cubiertos con frijoles, carne molida, queso fundido, pico de gallo y guacamole.',1,'Nachos Supremos',6500,NULL);
INSERT INTO MenuItems VALUES(4,'Plato fuerte','2025-11-16 16:12:48.2690936','La tradición tica: arroz, frijoles, plátano maduro, ensalada, tortilla y su elección de carne, pollo o pescado.',1,'Casado de la Casa',9500,NULL);
INSERT INTO MenuItems VALUES(5,'Plato fuerte','2025-11-16 16:13:17.0146875','Jugosa pechuga de pollo grillada, bañada en una salsa cremosa de mostaza y hierbas.',1,'Pechuga a la Mostaza',11200,NULL);
INSERT INTO MenuItems VALUES(6,'Plato fuerte','2025-11-16 16:13:51.2870899','Fetuccini en salsa Alfredo con una mezcla de vegetales frescos salteados (brócoli, zanahoria, chayote y champiñones).',1,'Pasta Primavera',10500,NULL);
INSERT INTO MenuItems VALUES(7,'Postre','2025-11-16 16:14:27.1057191','El clásico bizcocho esponjoso empapado en una mezcla de tres leches y coronado con merengue.',1,'Tres Leches',3800,NULL);
INSERT INTO MenuItems VALUES(8,'Postre','2025-11-16 16:14:52.6637549','Un pequeño pastel de chocolate con un corazón caliente y fundido, servido con una bola de helado de vainilla.',1,'Volcán de Chocolate',4500,NULL);
INSERT INTO MenuItems VALUES(9,'Postre','2025-11-16 16:15:16.8043307','Delicado flan de huevo con un intenso sabor a coco y un caramelo suave.',1,'Flan de Coco',3500,NULL);
INSERT INTO MenuItems VALUES(10,'Bebidas','2025-11-16 16:16:11.6092584','Refrescante bebida natural hecha a base de la fruta costarricense cas.',1,'Refresco de Cas',2000,NULL);
INSERT INTO MenuItems VALUES(11,'Bebidas','2025-11-16 16:16:43.9567217','Limonada naturalmente refrescante, endulzada al gusto.',1,'Limonada Fresca',2500,NULL);
INSERT INTO MenuItems VALUES(12,'Bebidas','2025-11-16 16:17:08.9606798','Espumoso batido natural de fresa con leche.',1,'Batido de Fresa',3200,NULL);
PRAGMA writable_schema=ON;
CREATE TABLE IF NOT EXISTS sqlite_sequence(name,seq);
DELETE FROM sqlite_sequence;
INSERT INTO sqlite_sequence VALUES('MenuItems',12);
PRAGMA writable_schema=OFF;
COMMIT;
