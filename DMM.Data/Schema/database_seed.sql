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


INSERT INTO FileType (FileExtension, IsArchive, isModFile)
SELECT '.bsa', 1, 1
WHERE NOT EXISTS (SELECT 1 FROM FileType WHERE LOWER(FileExtension) = '.bsa');

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


INSERT INTO FolderType (Name)
SELECT 'GameDataRelative'
WHERE NOT EXISTS (SELECT 1 FROM FolderType WHERE Name = 'GameDataRelative');

INSERT INTO FolderRole (Name, IncludeInBackup, IsRepoFolder, IsToolFolder)
SELECT 'GameDataRelative', 0, 0, 0
WHERE NOT EXISTS (SELECT 1 FROM FolderRole WHERE Name = 'GameDataRelative');

INSERT INTO Folders (Path, FolderTypeId, FolderRoleId, Description)
SELECT 'Data', ft.id, fr.id, 'Seeded relative data root for known core files.'
FROM FolderType ft
JOIN FolderRole fr ON fr.Name = 'GameDataRelative'
WHERE ft.Name = 'GameDataRelative'
  AND NOT EXISTS (SELECT 1 FROM Folders WHERE Path = 'Data');

INSERT INTO FileStorageKind (Name, Description)
SELECT 'Primary', 'Primary on-disk or canonical seeded file metadata.'
WHERE NOT EXISTS (SELECT 1 FROM FileStorageKind WHERE Name = 'Primary');

INSERT INTO FileInfo (Name, DTStamp, Size, GameId, RelativeFolderId, FileTypeId, FileStorageKindId)
SELECT DISTINCT
    files.FileName,
    '1970-01-01T00:00:00.0000000Z',
    0,
    g.id,
    dataFolder.id,
    ft.id,
    fsk.id
FROM (
    SELECT gk.GameId, gk.PluginName AS FileName
    FROM GameKnownPlugin gk

    UNION ALL

    SELECT g.id AS GameId, a.FileName
    FROM (
        SELECT 'Skyrim Special Edition' AS GameName, 'Skyrim - Misc.bsa' AS FileName
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Shaders.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Interface.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Animations.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Meshes0.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Meshes1.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Sounds.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Voices_en0.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures0.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures1.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures2.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures3.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures4.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures5.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures6.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures7.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Textures8.bsa'
        UNION ALL SELECT 'Skyrim Special Edition', 'Skyrim - Patch.bsa'

        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Startup.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Shaders.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Interface.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Voices.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Meshes.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - MeshesExtra.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Misc.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Sounds.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Materials.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Animations.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures1.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures2.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures3.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures4.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures5.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures6.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures7.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures8.ba2'
        UNION ALL SELECT 'Fallout 4', 'Fallout4 - Textures9.ba2'

        UNION ALL SELECT 'Starfield', 'Starfield - Animations.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - DensityMaps.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - FaceAnimation01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - FaceAnimation02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - FaceAnimation03.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - FaceAnimation04.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - FaceAnimationPatch.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - FaceMeshes.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - GeneratedTextures.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - LODMeshes.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - LODMeshesPatch.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Materials.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Meshes01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Meshes02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - MeshesPatch.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Misc.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Particles.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - PlanetData.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Terrain01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Terrain02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Terrain03.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Terrain04.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - TerrainPatch.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - LODTextures01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - LODTextures02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures03.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures04.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures05.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures06.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures07.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures08.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures09.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures10.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Textures11.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - TexturesPatch01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - TexturesPatch02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Interface.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Localization.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Shaders.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - ShadersBeta.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - WwiseSounds01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - WwiseSounds02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - WwiseSounds03.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - WwiseSounds04.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - WwiseSounds05.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - WwiseSoundsPatch.ba2'
        UNION ALL SELECT 'Starfield', 'BlueprintShips-Starfield - Localization.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Voices01.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - Voices02.ba2'
        UNION ALL SELECT 'Starfield', 'Starfield - VoicesPatch.ba2'
    ) a
    JOIN Game g ON g.Name = a.GameName
) files
JOIN Game g ON g.id = files.GameId
JOIN Folders dataFolder ON dataFolder.Path = 'Data'
JOIN FileStorageKind fsk ON fsk.Name = 'Primary'
LEFT JOIN FileType ft ON LOWER(ft.FileExtension) = LOWER(SUBSTR(files.FileName, INSTR(files.FileName, '.' )))
WHERE g.Name IN ('Starfield', 'Fallout 4', 'Skyrim Special Edition')
  AND ft.id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM FileInfo fi
      WHERE fi.Name = files.FileName
        AND IFNULL(fi.GameId, 0) = g.id
        AND IFNULL(fi.RelativeFolderId, 0) = dataFolder.id
  );

INSERT OR IGNORE INTO UrlRule (Name, URLRule) VALUES
('Steam-App', 'https://store.steampowered.com/app/{id}'),
('GamePass-Product', 'https://www.xbox.com/games/store/{slug}'),
('Epic-Product', 'https://store.epicgames.com/p/{slug}'),
('GoG-Product', 'https://www.gog.com/en/game/{slug}');
