PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS Waiters (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  RestaurantId INTEGER NOT NULL,
  Username TEXT NOT NULL,
  PasswordHash TEXT NOT NULL,
  CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE (RestaurantId, Username)
);


-- Password for all users is: password123
INSERT INTO Waiters (RestaurantId, Username, PasswordHash)
SELECT 1, 'carlos', '100000.8xN2KVZDqM+LgQH5vXNg5w==.rL9X8kBYhY8K9MqF7XJ6Y8bL5WvJ7kP8Y9Q3X2N6L5A='
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 1 AND Username = 'carlos');

INSERT INTO Waiters (RestaurantId, Username, PasswordHash)
SELECT 1, 'ana', '100000.8xN2KVZDqM+LgQH5vXNg5w==.rL9X8kBYhY8K9MqF7XJ6Y8bL5WvJ7kP8Y9Q3X2N6L5A='
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 1 AND Username = 'ana');

INSERT INTO Waiters (RestaurantId, Username, PasswordHash)
SELECT 2, 'miguel', '100000.8xN2KVZDqM+LgQH5vXNg5w==.rL9X8kBYhY8K9MqF7XJ6Y8bL5WvJ7kP8Y9Q3X2N6L5A='
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 2 AND Username = 'miguel');

COMMIT;
