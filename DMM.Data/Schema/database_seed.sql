-- Idempotent baseline seed data for DevModManager.
-- Safe to run repeatedly on existing databases.

BEGIN TRANSACTION;

INSERT INTO Game (Name, Executable)
SELECT 'Starfield', 'Starfield.exe'
WHERE NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Starfield');

INSERT INTO Game (Name, Executable)
SELECT 'Fallout 4', 'Fallout4.exe'
WHERE NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4');

INSERT INTO Game (Name, Executable)
SELECT 'Skyrim', 'SkyrimSE.exe'
WHERE NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Skyrim');

INSERT OR IGNORE INTO GameSource (Name, SourceGameId, URL, URI) VALUES
('Steam', NULL, 'https://store.steampowered.com/', 'steam://'),
('GamePass', NULL, 'https://www.xbox.com/xbox-game-pass', 'ms-xbox-gamepass://'),
('Epic', NULL, 'https://store.epicgames.com/', 'com.epicgames.launcher://'),
('GoG', NULL, 'https://www.gog.com/', 'goggalaxy://');

INSERT OR IGNORE INTO GameSource (Name, SourceGameId, URL, URI) VALUES
('Starfield-Steam', '1716740', 'https://store.steampowered.com/app/1716740', 'steam://run/1716740'),
('Fallout4-Steam', '377160', 'https://store.steampowered.com/app/377160', 'steam://run/377160'),
('Skyrim-Steam', '489830', 'https://store.steampowered.com/app/489830', 'steam://run/489830');

INSERT OR IGNORE INTO Platform (Name) VALUES
('PC-Steam'),
('PC-Gamepass'),
('PC-Epic'),
('PC-GoG'),
('XBox'),
('PSN'),
('Switch');

INSERT INTO Stages (StageName, IsSource, IsReserved)
SELECT 'DEV', 1, 1
WHERE NOT EXISTS (SELECT 1 FROM Stages WHERE StageName = 'DEV');

INSERT INTO Stages (StageName, IsSource, IsReserved)
SELECT 'TEST', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM Stages WHERE StageName = 'TEST');

INSERT INTO Stages (StageName, IsSource, IsReserved)
SELECT 'PREFLIGHT', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM Stages WHERE StageName = 'PREFLIGHT');

INSERT INTO Stages (StageName, IsSource, IsReserved)
SELECT 'PROD', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM Stages WHERE StageName = 'PROD');

INSERT INTO Stages (StageName, IsSource, IsReserved)
SELECT 'NEXUS', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM Stages WHERE StageName = 'NEXUS');

INSERT INTO Stages (StageName, IsSource, IsReserved)
SELECT 'CREATIONS', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM Stages WHERE StageName = 'CREATIONS');

INSERT INTO ExternalTools (Name)
SELECT 'Creation Kit'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'Creation Kit');

INSERT INTO ExternalTools (Name)
SELECT 'xEdit'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'xEdit');

INSERT INTO ExternalTools (Name)
SELECT 'NifSkope'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'NifSkope');

INSERT INTO ExternalTools (Name)
SELECT 'Vortex'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'Vortex');

INSERT INTO ExternalTools (Name)
SELECT 'Archive2'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'Archive2');

INSERT INTO ExternalTools (Name)
SELECT 'Elric'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'Elric');

INSERT INTO ExternalTools (Name)
SELECT 'Papyrus Compiler'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'Papyrus Compiler');

INSERT INTO ExternalTools (Name)
SELECT 'Asset Watcher'
WHERE NOT EXISTS (SELECT 1 FROM ExternalTools WHERE Name = 'Asset Watcher');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.nif', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.nif');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.mat', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.mat');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.psc', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.psc');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.pex', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.pex');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.ini', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.ini');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.exe', 0
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.exe');

INSERT INTO FileType (FileExtension, IsArchive, isModFile)
SELECT '.ba2', 1, 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.ba2');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.tif', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.tif');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.dds', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.dds');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.btd', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.btd');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.btc', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.btc');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.biom', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.biom');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.bk2', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.bk2');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.wav', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.wav');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.wem', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.wem');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.ffxanim', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.ffxanim');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.psfx', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.psfx');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.mesh', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.mesh');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.esm', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.esm');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.esp', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.esp');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.esl', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.esl');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.lip', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.lip');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.fuz', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.fuz');

INSERT INTO FileType (FileExtension, isModFile)
SELECT '.sfs', 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.sfs');

INSERT OR IGNORE INTO UrlRule (Name, URLRule) VALUES
('Steam-App', 'https://store.steampowered.com/app/{id}'),
('GamePass-Product', 'https://www.xbox.com/games/store/{slug}'),
('Epic-Product', 'https://store.epicgames.com/p/{slug}'),
('GoG-Product', 'https://www.gog.com/en/game/{slug}');

COMMIT;
