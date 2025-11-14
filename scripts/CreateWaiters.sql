PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS Waiters (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  RestaurantId INTEGER NOT NULL,
  Name TEXT NOT NULL,
  CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE (RestaurantId, Name)
);

-- Insert sample waiters for RestaurantId = 1 and 2
INSERT INTO Waiters (RestaurantId, Name)
SELECT 1, 'Carlos'
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 1 AND Name = 'Carlos');

INSERT INTO Waiters (RestaurantId, Name)
SELECT 1, 'Ana'
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 1 AND Name = 'Ana');

INSERT INTO Waiters (RestaurantId, Name)
SELECT 2, 'Miguel'
WHERE NOT EXISTS (SELECT 1 FROM Waiters WHERE RestaurantId = 2 AND Name = 'Miguel');

COMMIT;

-- End of file
