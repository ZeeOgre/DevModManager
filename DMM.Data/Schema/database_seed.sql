-- Idempotent baseline seed data for DevModManager.
-- Safe to run repeatedly on existing databases.

INSERT INTO Game (Name, Executable)
SELECT 'Starfield', 'Starfield.exe'
WHERE NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Starfield');

INSERT INTO Game (Name, Executable)
SELECT 'Fallout 4', 'Fallout4.exe'
WHERE NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4');

INSERT INTO Game (Name, Executable)
SELECT 'Skyrim Special Edition', 'SkyrimSE.exe'
WHERE NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Skyrim Special Edition');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Starfield - Shattered Space', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Starfield'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Starfield - Shattered Space');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Starfield - Old Mars', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Starfield'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Starfield - Old Mars');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Starfield - Constellation', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Starfield'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Starfield - Constellation');


INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Fallout 4: Automatron', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Fallout 4'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4: Automatron');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Fallout 4: Contraptions Workshop', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Fallout 4'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4: Contraptions Workshop');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Fallout 4: Far Harbor', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Fallout 4'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4: Far Harbor');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Fallout 4: High Resolution Texture Pack', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Fallout 4'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4: High Resolution Texture Pack');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Fallout 4: Nuka-World', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Fallout 4'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4: Nuka-World');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Fallout 4: Vault-Tec Workshop', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Fallout 4'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4: Vault-Tec Workshop');

INSERT INTO Game (Name, Executable, ParentGameId, IsBaseGame, IsDlc)
SELECT 'Fallout 4: Wasteland Workshop', '', g.id, 0, 1
FROM Game g
WHERE g.Name = 'Fallout 4'
  AND NOT EXISTS (SELECT 1 FROM Game WHERE Name = 'Fallout 4: Wasteland Workshop');

INSERT OR IGNORE INTO GameSource (Name, SourceGameId, URL, URI) VALUES
('Steam', NULL, 'https://store.steampowered.com/', 'steam://'),
('GamePass', NULL, 'https://www.xbox.com/xbox-game-pass', 'ms-xbox-gamepass://'),
('Epic', NULL, 'https://store.epicgames.com/', 'com.epicgames.launcher://'),
('GoG', NULL, 'https://www.gog.com/', 'goggalaxy://');

INSERT OR IGNORE INTO GameStoreApp (GameId, GameSourceId, StoreAppId)
SELECT g.id, s.id, app.StoreAppId
FROM (
    SELECT 'Starfield' AS GameName, 'Steam' AS SourceName, '1716740' AS StoreAppId
    UNION ALL SELECT 'Fallout 4', 'Steam', '377160'
    UNION ALL SELECT 'Skyrim Special Edition', 'Steam', '489830'
    UNION ALL SELECT 'Starfield', 'GamePass', 'BethesdaSoftworks.ProjectGold'
    UNION ALL SELECT 'Starfield - Shattered Space', 'GamePass', 'BethesdaSoftworks.ShatteredSpace'
    UNION ALL SELECT 'Starfield - Old Mars', 'GamePass', 'BethesdaSoftworks.PGPreorderContentwPkg'
    UNION ALL SELECT 'Starfield - Constellation', 'GamePass', 'BethesdaSoftworks.PGDeluxeContentwPkg'
    UNION ALL SELECT 'Fallout 4', 'GamePass', 'BethesdaSoftworks.Fallout4-CoreGame'
    UNION ALL SELECT 'Fallout 4: Automatron', 'GamePass', 'BethesdaSoftworks.Fallout4-DLC1Automatron'
    UNION ALL SELECT 'Fallout 4: Contraptions Workshop', 'GamePass', 'BethesdaSoftworks.Fallout4-DLC4ContraptionsWorksho'
    UNION ALL SELECT 'Fallout 4: Far Harbor', 'GamePass', 'BethesdaSoftworks.Fallout4-DLC3FarHarbor'
    UNION ALL SELECT 'Fallout 4: High Resolution Texture Pack', 'GamePass', 'BethesdaSoftworks.Fallout4HighResolutionTexturePac'
    UNION ALL SELECT 'Fallout 4: Nuka-World', 'GamePass', 'BethesdaSoftworks.Fallout4-DLC6Nuka-World'
    UNION ALL SELECT 'Fallout 4: Vault-Tec Workshop', 'GamePass', 'BethesdaSoftworks.Fallout4-DLC5Vault-TecWorkshop'
    UNION ALL SELECT 'Fallout 4: Wasteland Workshop', 'GamePass', 'BethesdaSoftworks.Fallout4-DLC2WastelandWorkshop'
) app
JOIN Game g ON g.Name = app.GameName
JOIN GameSource s ON s.Name = app.SourceName;

INSERT OR IGNORE INTO GameKnownPlugin (GameId, DisplayName, PluginName, IsBaseGame, IsDlc)
SELECT g.id, p.DisplayName, p.PluginName, p.IsBaseGame, p.IsDlc
FROM (
    SELECT 'Starfield' AS GameName, 'Trackers Alliance support' AS DisplayName, 'SFBGS003.esm' AS PluginName, 1 AS IsBaseGame, 0 AS IsDlc
    UNION ALL SELECT 'Starfield', 'Vehicle / REV-8', 'SFBGS004.esm', 1, 0
    UNION ALL SELECT 'Starfield', 'Ship Decoration', 'SFBGS006.esm', 1, 0
    UNION ALL SELECT 'Starfield', 'Gameplay Options', 'SFBGS007.esm', 1, 0
    UNION ALL SELECT 'Starfield', 'City Maps Data', 'SFBGS008.esm', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Makeshift Weapon Pack - When Pigs Fly', 'ccSBJFO4003-Grenade.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Halloween Workshop Pack - All Hallows'' Eve', 'ccFSVFO4007-Halloween.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Enclave Remnants - Echoes of the Past', 'ccOTMFO4001-Remnants.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Tesla Cannon - Best of Three', 'ccBGSFO4046-TesCan.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Hellfire Power Armor - Pyromaniac', 'ccBGSFO4044-HellfirePowerArmor.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'X-02 Power Armor - Speak of the Devil', 'ccBGSFO4115-X02.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Heavy Incinerator - Crucible', 'ccBGSFO4116-HeavyFlamer.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Enclave Armor Skins', 'ccBGSFO4096-AS_Enclave.esl', 1, 0
    UNION ALL SELECT 'Fallout 4', 'Enclave Weapon Skins', 'ccBGSFO4110-WS_Enclave.esl', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Dawnguard', 'Dawnguard.esm', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Hearthfire', 'HearthFires.esm', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Dragonborn', 'Dragonborn.esm', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Saints & Seducers', 'ccBGSSSE025-AdvDSGS.esm', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Rare Curios', 'ccBGSSSE037-Curios.esl', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Survival Mode', 'ccQDRSSE001-SurvivalMode.esl', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Fishing', 'ccBGSSSE001-Fish.esm', 1, 0
    UNION ALL SELECT 'Skyrim Special Edition', 'Resource Pack', '_ResourcePack.esl', 1, 0
    UNION ALL SELECT 'Starfield - Shattered Space', 'Shattered Space', 'ShatteredSpace.esm', 0, 1
    UNION ALL SELECT 'Fallout 4: Automatron', 'Automatron', 'DLCRobot.esm', 0, 1
    UNION ALL SELECT 'Fallout 4: Contraptions Workshop', 'Contraptions Workshop', 'DLCworkshop02.esm', 0, 1
    UNION ALL SELECT 'Fallout 4: Far Harbor', 'Far Harbor', 'DLCCoast.esm', 0, 1
    UNION ALL SELECT 'Fallout 4: Nuka-World', 'Nuka-World', 'DLCNukaWorld.esm', 0, 1
    UNION ALL SELECT 'Fallout 4: Vault-Tec Workshop', 'Vault-Tec Workshop', 'DLCworkshop03.esm', 0, 1
    UNION ALL SELECT 'Fallout 4: Wasteland Workshop', 'Wasteland Workshop', 'DLCworkshop01.esm', 0, 1
) p
JOIN Game g ON g.Name = p.GameName;

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
