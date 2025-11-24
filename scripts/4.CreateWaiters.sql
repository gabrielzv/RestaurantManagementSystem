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

-- Insert sample waiters for RestaurantId = 1 and 2
INSERT INTO Waiters (RestaurantId, Username, PasswordHash)
SELECT 1, 'carlos', 'AQAAAAEAACcQAAAAEBLjouNqAeNrMZtq7hSxgGF2dFMHkq3R8zYQH8Q2dQkJ5QK8qQ=='
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 1 AND Username = 'carlos');

INSERT INTO Waiters (RestaurantId, Username, PasswordHash)
SELECT 1, 'ana', 'AQAAAAEAACcQAAAAEBLjouNqAeNrMZtq7hSxgGF2dFMHkq3R8zYQH8Q2dQkJ5QK8qQ=='
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 1 AND Username = 'ana');

INSERT INTO Waiters (RestaurantId, Username, PasswordHash)
SELECT 2, 'miguel', 'AQAAAAEAACcQAAAAEBLjouNqAeNrMZtq7hSxgGF2dFMHkq3R8zYQH8Q2dQkJ5QK8qQ=='
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 2 AND Username = 'miguel');

COMMIT;

-- End of file
