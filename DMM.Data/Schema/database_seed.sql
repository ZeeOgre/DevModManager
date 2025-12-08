--
-- File generated with SQLiteStudio v3.4.18 on Sun Dec 7 19:50:19 2025
--
-- Text encoding used: System
--
PRAGMA foreign_keys = off;
BEGIN TRANSACTION;

-- Table: ActiveMod
DROP TABLE IF EXISTS ActiveMod;

CREATE TABLE IF NOT EXISTS ActiveMod (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    GameProfileId INTEGER NOT NULL
                          REFERENCES GameProfile (id) ON DELETE CASCADE
                                                      ON UPDATE CASCADE,
    ModId         INTEGER NOT NULL
                          REFERENCES ModItems (id) ON DELETE CASCADE
                                                   ON UPDATE CASCADE,
    UNIQUE (
        GameProfileId
    )
);


-- Table: AssetHandler
DROP TABLE IF EXISTS AssetHandler;

CREATE TABLE IF NOT EXISTS AssetHandler (
    id     INTEGER PRIMARY KEY AUTOINCREMENT,
    Class  TEXT    NOT NULL,
    ToolId INTEGER REFERENCES ExternalTools (id) ON DELETE SET NULL
                                                 ON UPDATE CASCADE
);


-- Table: AssetKind
DROP TABLE IF EXISTS AssetKind;

CREATE TABLE IF NOT EXISTS AssetKind (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    Name               TEXT    UNIQUE
                               NOT NULL,
    DefaultArchiveName TEXT,
    DefaultDataPath    TEXT
);

INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (1, 'Animations', 'Main', 'meshes');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (2, 'DensityMaps', 'Textures', 'textures/terrain');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (3, 'FaceAnimation', 'Main', 'sound/voice');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (4, 'FaceMeshes', 'Main', 'meshes');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (5, 'GeneratedTextures', 'Textures', 'textures/interface');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (6, 'Meshes - NIF', 'Main', 'meshes');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (7, 'Geometries - MESH', 'Main', 'geometries');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (8, 'Interface', 'Main', 'interface');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (9, 'LODMeshes', 'Main', 'meshes/lod');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (10, 'Localization', 'Main', 'strings');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (11, 'Materials', 'Main', 'materials');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (12, 'Misc', 'Main', 'misc');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (13, 'Scripts', 'Main', 'scripts');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (14, 'ScriptSource', 'Main', 'scripts/source');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (15, 'Particles', 'Main', 'particles');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (16, 'PlanetData', 'Main', 'planetdata/biomemaps');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (17, 'Terrain', 'Main', 'terrain');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (18, 'Terrain Chunk', NULL, 'terrain');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (19, 'Terrain Overlay Source Image', NULL, 'Source/TGATextures/Terrain/OverlayMasks');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (20, 'Textures', 'Textures', 'textures');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (21, 'InventoryIcon', 'Textures', 'textures/interface/InventoryIcons');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (22, 'WorkshopIcon', 'Textures', 'textures/interface/ShipBuilderIcons');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (23, 'ShipBuilderIcon', 'Textures', 'textures/interface/WorkshopIcons');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (24, 'OverlayMasks', 'Textures', 'textures/terrain/OverlayMasks');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (25, 'Video', NULL, 'video');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (26, 'Voices', 'Main', 'sound/voice');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (27, 'Voice Source', NULL, 'sound/voice');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (28, 'Source Image', NULL, '../../../Source/TGATextures');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (29, 'WwiseSounds', 'Main', 'sound/soundbanks');
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (30, 'ArchiveMain', NULL, NULL);
INSERT INTO AssetKind (id, Name, DefaultArchiveName, DefaultDataPath) VALUES (31, 'ArchiveTextures', NULL, NULL);

-- Table: Config
DROP TABLE IF EXISTS Config;

CREATE TABLE IF NOT EXISTS Config (
    id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigName            TEXT,
    LocalRepoFolderId     INTEGER REFERENCES Folders (id) ON DELETE CASCADE
                                                          ON UPDATE CASCADE,
    UseGit                INTEGER DEFAULT (1) 
                                  NOT NULL
                                  CONSTRAINT bool CHECK (UseGit IN (0, 1) ),
    GitHubRepo            TEXT,
    GitHubUsername        TEXT,
    GitHubToken           TEXT,
    GitHubTokenExpiration TEXT,
    GitEachModSeparate    INTEGER CONSTRAINT bool CHECK (GitEachModSeparate IN (0, 1) ) 
                                  NOT NULL
                                  DEFAULT (1),
    UseModManager         INTEGER DEFAULT (1) 
                                  NOT NULL
                                  CONSTRAINT bool CHECK (UseModManager IN (0, 1) ),
    DefaultGameProfile    INTEGER REFERENCES GameProfile (id) ON DELETE SET NULL
                                                              ON UPDATE CASCADE,
    DefaultArchiveFormat  INTEGER REFERENCES FileType (id) ON DELETE SET NULL
                                                           ON UPDATE CASCADE,
    TimestampFormat       TEXT,
    MyNameSpace           TEXT,
    MyResourcePrefix      TEXT,
    ShowSaveMessage       INTEGER DEFAULT (0) 
                                  CHECK (ShowSaveMessage IN (0, 1) ),
    ShowOverwriteMessage  INTEGER DEFAULT (0) 
                                  NOT NULL
                                  CHECK (ShowOverwriteMessage IN (0, 1) ),
    NexusAPIKey           TEXT,
    AutoCheckForUpdates   INTEGER DEFAULT (1) 
                                  CHECK (AutoCheckForUpdates IN (0, 1) ) 
                                  NOT NULL,
    DarkMode              INTEGER DEFAULT (1) 
                                  CHECK (DarkMode IN (0, 1) ) 
                                  NOT NULL
);


-- Table: ExternalIds
DROP TABLE IF EXISTS ExternalIds;

CREATE TABLE IF NOT EXISTS ExternalIds (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    modId      INTEGER REFERENCES ModItems (id) ON DELETE CASCADE
                                                ON UPDATE CASCADE
                       NOT NULL,
    BethesdaId TEXT    NULL
                       UNIQUE,
    NexusId    TEXT    NULL
                       UNIQUE
);


-- Table: ExternalTools
DROP TABLE IF EXISTS ExternalTools;

CREATE TABLE IF NOT EXISTS ExternalTools (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                TEXT,
    baseFolderId        INTEGER REFERENCES Folders (id) ON DELETE SET NULL
                                                        ON UPDATE CASCADE,
    deployFolderId      INTEGER REFERENCES Folders (id) ON DELETE SET NULL
                                                        ON UPDATE CASCADE,
    Arguments           TEXT,
    AdditionalArguments INTEGER CONSTRAINT bool CHECK (AdditionalArguments IN (0, 1) ) 
                                DEFAULT (0),
    CheckDeploy         INTEGER CONSTRAINT bool CHECK (CheckDeploy IN (0, 1) ) 
                                DEFAULT (0) 
                                NOT NULL
);


-- Table: FileDependency
DROP TABLE IF EXISTS FileDependency;

CREATE TABLE IF NOT EXISTS FileDependency (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    FileInfoId          INTEGER REFERENCES FileInfo (id) ON DELETE CASCADE
                                                         ON UPDATE CASCADE,
    DependentFileInfoId INTEGER REFERENCES FileInfo (id) ON DELETE CASCADE
                                                         ON UPDATE CASCADE
);


-- Table: FileFolderDeploymentState
DROP TABLE IF EXISTS FileFolderDeploymentState;

CREATE TABLE IF NOT EXISTS FileFolderDeploymentState (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    FileInfoId    INTEGER REFERENCES FileInfo (id) ON DELETE CASCADE
                                                   ON UPDATE CASCADE,
    FolderId      INTEGER REFERENCES Folders (id) ON DELETE CASCADE
                                                  ON UPDATE CASCADE,
    GameProfileId INTEGER REFERENCES GameProfile (id) ON DELETE CASCADE
                                                      ON UPDATE CASCADE
);


-- Table: FileInfo
DROP TABLE IF EXISTS FileInfo;

CREATE TABLE IF NOT EXISTS FileInfo (
    id               INTEGER  PRIMARY KEY AUTOINCREMENT,
    Name             TEXT     NOT NULL,
    DTStamp          DATETIME NOT NULL,
    Size             NUMERIC  NOT NULL,
    Hash             BLOB     NULL,
    GameId           INTEGER  REFERENCES Game (id) ON DELETE RESTRICT
                                                   ON UPDATE CASCADE,
    ArchiveFileId    INTEGER  REFERENCES FileInfo (id) ON DELETE SET NULL
                                                       ON UPDATE CASCADE,
    JunctionSourceId INTEGER  REFERENCES FileInfo (id) ON DELETE SET NULL
                                                       ON UPDATE CASCADE,
    RelativeFolderId INTEGER  NULL
                              REFERENCES Folders (id) ON DELETE SET NULL
                                                      ON UPDATE CASCADE,
    FileTypeId       INTEGER  REFERENCES FileType (id) ON DELETE SET NULL
                                                       ON UPDATE CASCADE,
    ModId            INTEGER  NULL
                              REFERENCES ModItems (id) ON DELETE SET NULL
                                                       ON UPDATE CASCADE
);


-- Table: FileStage
DROP TABLE IF EXISTS FileStage;

CREATE TABLE IF NOT EXISTS FileStage (
    FileInfoId INTEGER NOT NULL,
    StageId    INTEGER NOT NULL,
    CONSTRAINT PK_FileStage PRIMARY KEY (
        FileInfoId,
        StageId
    ),
    CONSTRAINT FK_FileStage_StageId FOREIGN KEY (
        StageId
    )
    REFERENCES Stages (id) ON DELETE NO ACTION
                           ON UPDATE NO ACTION,
    CONSTRAINT FK_FileStage_FileInfoId FOREIGN KEY (
        FileInfoId
    )
    REFERENCES FileInfo (id) ON DELETE NO ACTION
                             ON UPDATE NO ACTION
);


-- Table: FileType
DROP TABLE IF EXISTS FileType;

CREATE TABLE IF NOT EXISTS FileType (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    FileExtension TEXT    NOT NULL,
    Regex         TEXT,
    IsArchive     INTEGER CONSTRAINT bool CHECK (IsArchive IN (0, 1) ) 
                          DEFAULT (0),
    ExcludeBA2    INTEGER CONSTRAINT bool CHECK (ExcludeBA2 IN (0, 1) ) 
                          DEFAULT (1),
    isModFile     INTEGER CONSTRAINT bool CHECK (isModFile IN (0, 1) ) 
                          DEFAULT (0),
    ExcludeBackup INTEGER CONSTRAINT bool CHECK (ExcludeBackup IN (0, 1) ) 
                          DEFAULT (0),
    PlatformId    INTEGER REFERENCES Platform (id) ON DELETE RESTRICT
                                                   ON UPDATE CASCADE,
    GameProfileId INTEGER REFERENCES GameProfile (id) ON DELETE RESTRICT
                                                      ON UPDATE CASCADE,
    AssetKindId   INTEGER REFERENCES AssetKind (id) ON DELETE SET NULL
                                                    ON UPDATE CASCADE
);

INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (1, 'esm', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (2, 'esp', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (3, 'esl', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (4, 'ba2', NULL, 1, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (5, 'pex', NULL, 0, 0, 0, 0, NULL, NULL, 13);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (6, 'psc', NULL, 0, 1, 0, 0, NULL, NULL, 14);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (7, 'nif', NULL, 0, 0, 0, 0, NULL, NULL, 6);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (8, 'mesh', NULL, 0, 0, 0, 0, NULL, NULL, 7);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (9, 'btd', NULL, 0, 0, 0, 0, NULL, NULL, 17);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (10, 'dds', NULL, 0, 0, 0, 0, NULL, NULL, 20);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (11, 'tif', NULL, 0, 1, 0, 0, NULL, NULL, 28);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (12, 'png', NULL, 0, 1, 0, 0, NULL, NULL, 28);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (13, 'jpg', NULL, 0, 1, 0, 0, NULL, NULL, 28);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (14, 'mat', NULL, 0, 0, 0, 0, NULL, NULL, 11);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (15, 'wem', NULL, 0, 0, 0, 0, NULL, NULL, 29);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (16, 'ffxanim', NULL, 0, 0, 0, 0, NULL, NULL, 3);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (17, 'wav', NULL, 0, 1, 0, 0, NULL, NULL, 27);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (18, 'btc', NULL, 0, 1, 0, 0, NULL, NULL, 18);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (19, 'ini', NULL, 0, 1, 0, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (20, 'toml', NULL, 0, 1, 0, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (21, 'cfg', NULL, 0, 1, 0, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (22, 'txt', NULL, 0, 1, 0, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (23, 'log', NULL, 0, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (24, 'dmp', NULL, 0, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (25, 'sfs', NULL, 0, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (26, 'zip', NULL, 1, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (27, '7z', NULL, 1, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (28, 'rar', NULL, 1, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (29, 'exe', NULL, 0, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (30, 'dll', NULL, 0, 1, 0, 1, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (31, 'json', NULL, 0, 1, 0, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (32, 'xml', NULL, 0, 1, 0, 0, NULL, NULL, NULL);

-- Table: FileTypeAssetHandler
DROP TABLE IF EXISTS FileTypeAssetHandler;

CREATE TABLE IF NOT EXISTS FileTypeAssetHandler (
    FileTypeId     INTEGER NOT NULL
                           REFERENCES FileType (id) ON DELETE CASCADE
                                                    ON UPDATE CASCADE,
    AssetHandlerId INTEGER NOT NULL
                           REFERENCES AssetHandler (id) ON DELETE CASCADE
                                                        ON UPDATE CASCADE,
    PRIMARY KEY (
        FileTypeId,
        AssetHandlerId
    )
);


-- Table: FolderRole
DROP TABLE IF EXISTS FolderRole;

CREATE TABLE IF NOT EXISTS FolderRole (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    Name              TEXT    UNIQUE
                              NOT NULL,
    IncludeInBackup   INTEGER NOT NULL
                              DEFAULT 1
                              CHECK (IncludeInBackup IN (0, 1) ),
    PlatformId        INTEGER REFERENCES Platform (id) ON DELETE RESTRICT
                                                       ON UPDATE CASCADE,
    IsRepoFolder      INTEGER NOT NULL
                              DEFAULT 0
                              CHECK (IsRepoFolder IN (0, 1) ),
    IsToolFolder      INTEGER NOT NULL
                              DEFAULT 0
                              CHECK (IsToolFolder IN (0, 1) ),
    isPlatformSpecifc INTEGER GENERATED ALWAYS AS (PlatformId > 0) 
);

INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (0, 'GameRoot', 0, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (1, 'DataRoot', 1, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (2, 'XBoxDataRoot', 1, 1, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (3, 'PSNDataRoot', 1, 2, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (4, 'SwitchDataRoot', 1, 4, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (5, 'SteamRoot', 0, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (6, 'ScriptRoot', 1, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (7, 'ToolRoot', 0, 0, 0, 1);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (8, 'BackupRoot', 0, 0, 1, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (9, 'RepoRoot', 0, 0, 1, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (10, 'RepoModuleRoot', 0, 0, 1, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (11, 'UserMyGames', 1, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (12, 'UserLocalAppData', 1, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (13, 'UserSaves', 0, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (14, 'UserLogs', 0, 0, 0, 0);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (15, 'AppRoot', 0, 0, 0, 1);
INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (16, 'AppLogs', 0, 0, 0, 1);

-- Table: Folders
DROP TABLE IF EXISTS Folders;

CREATE TABLE IF NOT EXISTS Folders (
    id               INTEGER PRIMARY KEY AUTOINCREMENT
                             UNIQUE
                             NOT NULL,
    Path             TEXT    UNIQUE
                             NOT NULL,
    Description      TEXT    NULL,
    ParentFolderId   INTEGER NULL
                             REFERENCES Folders (id) ON DELETE CASCADE
                                                     ON UPDATE CASCADE,
    JunctionSourceId INTEGER NULL
                             REFERENCES Folders (id) ON DELETE SET NULL
                                                     ON UPDATE CASCADE,
    FolderTypeId     INTEGER NOT NULL
                             REFERENCES FolderType (id) ON DELETE CASCADE
                                                        ON UPDATE CASCADE,
    FolderRoleId     INTEGER NOT NULL
                             REFERENCES FolderRole (id) ON DELETE CASCADE
                                                        ON UPDATE CASCADE
);


-- Table: FolderType
DROP TABLE IF EXISTS FolderType;

CREATE TABLE IF NOT EXISTS FolderType (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT    UNIQUE
                 NOT NULL
);

INSERT INTO FolderType (id, Name) VALUES (0, 'GameRoot');
INSERT INTO FolderType (id, Name) VALUES (1, 'DataRoot');
INSERT INTO FolderType (id, Name) VALUES (2, 'XBoxDataRoot');
INSERT INTO FolderType (id, Name) VALUES (3, 'PSNDataRoot');
INSERT INTO FolderType (id, Name) VALUES (4, 'SwitchDataRoot');
INSERT INTO FolderType (id, Name) VALUES (5, 'SteamRoot');
INSERT INTO FolderType (id, Name) VALUES (6, 'ToolRoot');
INSERT INTO FolderType (id, Name) VALUES (7, 'BackupRoot');
INSERT INTO FolderType (id, Name) VALUES (8, 'RepoRoot');
INSERT INTO FolderType (id, Name) VALUES (9, 'RepoModuleRoot');
INSERT INTO FolderType (id, Name) VALUES (10, 'UserMyGames');
INSERT INTO FolderType (id, Name) VALUES (11, 'UserLocalAppData');
INSERT INTO FolderType (id, Name) VALUES (12, 'UserSaves');
INSERT INTO FolderType (id, Name) VALUES (13, 'GameLogs');
INSERT INTO FolderType (id, Name) VALUES (14, 'AppRoot');
INSERT INTO FolderType (id, Name) VALUES (15, 'AppLogs');

-- Table: FolderTypeFiles
DROP TABLE IF EXISTS FolderTypeFiles;

CREATE TABLE IF NOT EXISTS FolderTypeFiles (
    FolderTypeId INTEGER NOT NULL,
    FileTypeId   INTEGER NOT NULL,
    PRIMARY KEY (
        FolderTypeId,
        FileTypeId
    ),
    FOREIGN KEY (
        FolderTypeId
    )
    REFERENCES FolderType (id) ON DELETE CASCADE
                               ON UPDATE CASCADE,
    FOREIGN KEY (
        FileTypeId
    )
    REFERENCES FileType (id) ON DELETE CASCADE
                             ON UPDATE CASCADE
);


-- Table: FolderTypeKinds
DROP TABLE IF EXISTS FolderTypeKinds;

CREATE TABLE IF NOT EXISTS FolderTypeKinds (
    FolderTypeId INTEGER NOT NULL,
    AssetKindId  INTEGER NOT NULL,
    PRIMARY KEY (
        FolderTypeId,
        AssetKindId
    ),
    FOREIGN KEY (
        FolderTypeId
    )
    REFERENCES FolderType (id) ON DELETE CASCADE
                               ON UPDATE CASCADE,
    FOREIGN KEY (
        AssetKindId
    )
    REFERENCES AssetKind (id) ON DELETE CASCADE
                              ON UPDATE CASCADE
);


-- Table: FolderTypeRoles
DROP TABLE IF EXISTS FolderTypeRoles;

CREATE TABLE IF NOT EXISTS FolderTypeRoles (
    FolderRoleId INTEGER NOT NULL,
    FileTypeId   INTEGER NOT NULL,
    PRIMARY KEY (
        FolderRoleId,
        FileTypeId
    ),
    FOREIGN KEY (
        FolderRoleId
    )
    REFERENCES FolderRole (id) ON DELETE CASCADE
                               ON UPDATE CASCADE,
    FOREIGN KEY (
        FileTypeId
    )
    REFERENCES FileType (id) ON DELETE CASCADE
                             ON UPDATE CASCADE
);


-- Table: Game
DROP TABLE IF EXISTS Game;

CREATE TABLE IF NOT EXISTS Game (
    id                             INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                           TEXT    NOT NULL,
    Executable                     TEXT,
    NexusKeyword                   TEXT,
    BethesdaKeyword                TEXT,
    GameSettingsFolderId           INTEGER NULL
                                           REFERENCES Folders (id) ON DELETE SET NULL
                                                                   ON UPDATE CASCADE,
    GameSaveFolderId               INTEGER NULL
                                           REFERENCES Folders (id) ON DELETE SET NULL
                                                                   ON UPDATE CASCADE,
    GameLocalSettingsFolderId      INTEGER NULL
                                           REFERENCES Folders (id) ON DELETE SET NULL
                                                                   ON UPDATE CASCADE,
    GamePluginsTxtId               INTEGER NULL
                                           REFERENCES FileInfo (id) ON DELETE SET NULL
                                                                    ON UPDATE CASCADE,
    GameSettingsIniId              INTEGER NULL
                                           REFERENCES FileInfo (id) ON DELETE SET NULL
                                                                    ON UPDATE CASCADE,
    GameCustomSettingsIniId        INTEGER NULL
                                           REFERENCES FileInfo (id) ON DELETE SET NULL
                                                                    ON UPDATE CASCADE,
    CreationKitSettingsIniId       INTEGER NULL
                                           REFERENCES FileInfo (id) ON DELETE SET NULL
                                                                    ON UPDATE CASCADE,
    CreationKitCustomSettingsIniId INTEGER NULL
                                           REFERENCES FileInfo (id) ON DELETE SET NULL
                                                                    ON UPDATE CASCADE
);


-- Table: GameProfile
DROP TABLE IF EXISTS GameProfile;

CREATE TABLE IF NOT EXISTS GameProfile (
    id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                    TEXT    NOT NULL,
    GameId                  INTEGER NOT NULL
                                    REFERENCES Game (id) ON DELETE CASCADE
                                                         ON UPDATE CASCADE,
    GameFolderId            INTEGER NOT NULL
                                    REFERENCES Folders (id) ON DELETE CASCADE
                                                            ON UPDATE CASCADE,
    GameVersionId           INTEGER REFERENCES GameVersion (id) ON DELETE RESTRICT
                                                                ON UPDATE CASCADE,
    GameDataFolderId        INTEGER NULL
                                    REFERENCES Folders (id) ON DELETE SET NULL
                                                            ON UPDATE CASCADE,
    GameXboxFolderId        INTEGER NULL
                                    REFERENCES Folders (id) ON DELETE SET NULL
                                                            ON UPDATE CASCADE,
    GamePlaystationFolderId INTEGER REFERENCES Folders (id) ON DELETE RESTRICT
                                                            ON UPDATE CASCADE,
    TifFolderId             INTEGER NULL
                                    REFERENCES Folders (id) ON DELETE SET NULL
                                                            ON UPDATE CASCADE,
    GameVersionRepoId       INTEGER REFERENCES ModRepository (id) ON DELETE SET NULL
                                                                  ON UPDATE CASCADE
);


-- Table: GameSource
DROP TABLE IF EXISTS GameSource;

CREATE TABLE IF NOT EXISTS GameSource (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Name         TEXT,
    SourceGameId TEXT,
    URL          TEXT,
    URI          TEXT,
    SourceRepoId TEXT
);

INSERT INTO GameSource (id, Name, SourceGameId, URL, URI, SourceRepoId) VALUES (1, 'Starfield - Steam', '1716740', 'https://store.steampowered.com/app/1716740/Starfield/', 'steam://rungameid/1716740', NULL);

-- Table: GameVersion
DROP TABLE IF EXISTS GameVersion;

CREATE TABLE IF NOT EXISTS GameVersion (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Version     TEXT,
    GameId      INTEGER REFERENCES Game (id) ON DELETE CASCADE
                                             ON UPDATE CASCADE,
    GameSource  INTEGER REFERENCES GameSource (id) ON DELETE CASCADE
                                                   ON UPDATE CASCADE,
    VersionRepo TEXT
);


-- Table: GameVersionFiles
DROP TABLE IF EXISTS GameVersionFiles;

CREATE TABLE IF NOT EXISTS GameVersionFiles (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    FileId        INTEGER REFERENCES FileInfo (id) ON DELETE RESTRICT
                                                   ON UPDATE CASCADE,
    ArchiveId     INTEGER REFERENCES FileInfo (id),
    GameVersionId INTEGER REFERENCES GameVersion (id) ON DELETE RESTRICT
                                                      ON UPDATE CASCADE,
    ModId         INTEGER REFERENCES ModItems (id) ON DELETE RESTRICT
                                                   ON UPDATE CASCADE
);


-- Table: InitializationStatus
DROP TABLE IF EXISTS InitializationStatus;

CREATE TABLE IF NOT EXISTS InitializationStatus (
    id                 INTEGER NOT NULL,
    IsInitialized      INTEGER NOT NULL,
    InitializationTime TEXT    NOT NULL,
    CONSTRAINT sqlite_autoindex_InitializationStatus_1 PRIMARY KEY (
        id
    )
);


-- Table: ModItems
DROP TABLE IF EXISTS ModItems;

CREATE TABLE IF NOT EXISTS ModItems (
    id             INTEGER PRIMARY KEY AUTOINCREMENT
                           NOT NULL,
    ModName        TEXT    NOT NULL,
    ModFolderPath  TEXT    NOT NULL,
    CurrentStageID INTEGER NULL,
    isBaseline     INTEGER CHECK (isBaseline IN (0, 1) ) 
                           NOT NULL
                           DEFAULT (0),
    isManaged      INTEGER CONSTRAINT bool CHECK (isManaged IN (0, 1) ) 
                           NOT NULL
                           DEFAULT (0),
    CONSTRAINT FK_ModItems_0_0 FOREIGN KEY (
        CurrentStageID
    )
    REFERENCES Stages (id) ON DELETE SET NULL
                           ON UPDATE NO ACTION
);


-- Table: ModRepoFolder
DROP TABLE IF EXISTS ModRepoFolder;

CREATE TABLE IF NOT EXISTS ModRepoFolder (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    RepoFolderPathId    INTEGER NOT NULL
                                REFERENCES Folders (id) ON DELETE CASCADE
                                                        ON UPDATE CASCADE,
    SubModuleUrl        TEXT    NULL,
    LinkedFilesFolderId INTEGER NOT NULL
                                REFERENCES Folders (id) ON DELETE RESTRICT
                                                        ON UPDATE CASCADE,
    BackupFolderId      INTEGER NOT NULL
                                REFERENCES Folders (id) ON DELETE RESTRICT
                                                        ON UPDATE CASCADE,
    MetaFilesFolderId   INTEGER NOT NULL
                                REFERENCES Folders (id) ON DELETE RESTRICT
                                                        ON UPDATE CASCADE
);


-- Table: ModRepository
DROP TABLE IF EXISTS ModRepository;

CREATE TABLE IF NOT EXISTS ModRepository (
    id              INTEGER PRIMARY KEY AUTOINCREMENT
                            NOT NULL,
    ModId           INTEGER NOT NULL
                            REFERENCES ModItems (id) ON DELETE CASCADE
                                                     ON UPDATE CASCADE,
    RepoUrl         TEXT    NOT NULL,
    ParentRepoId    INTEGER REFERENCES ModRepository (id) ON DELETE RESTRICT
                                                          ON UPDATE CASCADE,
    isDLC           INTEGER NOT NULL
                            DEFAULT (0) 
                            CHECK (isDLC IN (0, 1) ),
    isBaseline      INTEGER NOT NULL
                            CONSTRAINT bool CHECK (isBaseline IN (0, 1) ) 
                            DEFAULT (0),
    LocalFolderId   INTEGER REFERENCES Folders (id) ON DELETE SET NULL
                                                    ON UPDATE CASCADE,
    IsSubmodule     INTEGER NOT NULL
                            CONSTRAINT bool CHECK (IsSubmodule IN (0, 1) ) 
                            DEFAULT (1),
    DefaultBranchId INTEGER REFERENCES StageBranchMapping (id) ON DELETE CASCADE
                                                               ON UPDATE CASCADE
);


-- Table: ModStages
DROP TABLE IF EXISTS ModStages;

CREATE TABLE IF NOT EXISTS ModStages (
    ModId   INTEGER NOT NULL,
    StageID INTEGER NOT NULL,
    CONSTRAINT sqlite_autoindex_ModStages_1 PRIMARY KEY (
        ModId,
        StageID
    ),
    CONSTRAINT FK_ModStages_0_0 FOREIGN KEY (
        StageID
    )
    REFERENCES Stages (id) ON DELETE NO ACTION
                           ON UPDATE NO ACTION,
    CONSTRAINT FK_ModStages_1_0 FOREIGN KEY (
        ModId
    )
    REFERENCES ModItems (id) ON DELETE NO ACTION
                             ON UPDATE NO ACTION
);


-- Table: Platform
DROP TABLE IF EXISTS Platform;

CREATE TABLE IF NOT EXISTS Platform (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    Name               TEXT    UNIQUE,
    ArchiveNamePattern TEXT
);

INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (0, 'PC', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (1, 'XBox', '_xbox');
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (2, 'PlayStation', '_psn');
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (3, 'SteamDeck', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (4, 'Nintendo Switch', '_switch');

-- Table: ReservedFolderNames
DROP TABLE IF EXISTS ReservedFolderNames;

CREATE TABLE IF NOT EXISTS ReservedFolderNames (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT    UNIQUE
                 NOT NULL
);


-- Table: StageBranchMapping
DROP TABLE IF EXISTS StageBranchMapping;

CREATE TABLE IF NOT EXISTS StageBranchMapping (
    id              INTEGER PRIMARY KEY AUTOINCREMENT
                            NOT NULL,
    ModRepositoryId INTEGER NOT NULL
                            REFERENCES ModRepository (id) ON DELETE CASCADE
                                                          ON UPDATE CASCADE,
    StageId         INTEGER NOT NULL
                            REFERENCES Stages (id) ON DELETE CASCADE
                                                   ON UPDATE CASCADE,
    BranchName      TEXT    NOT NULL,
    UNIQUE (
        ModRepositoryId,
        StageId
    )
);


-- Table: Stages
DROP TABLE IF EXISTS Stages;

CREATE TABLE IF NOT EXISTS Stages (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    StageName  TEXT    NOT NULL,
    IsSource   INTEGER DEFAULT (0) 
                       NULL,
    IsReserved INTEGER DEFAULT (0) 
                       NULL
);

INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (0, 'DEV', 1, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (1, 'PROD', 1, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (2, 'TEST', 1, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (3, 'STAGING', 1, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (4, 'CREATIONS', 0, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (5, 'NEXUS', 0, 1);

-- Table: ToolDeploymentState
DROP TABLE IF EXISTS ToolDeploymentState;

CREATE TABLE IF NOT EXISTS ToolDeploymentState (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ExternalToolId INTEGER REFERENCES ExternalTools (id) ON DELETE CASCADE
                                                         ON UPDATE CASCADE,
    GameProfileId  INTEGER REFERENCES GameProfile (id) ON DELETE CASCADE
                                                       ON UPDATE CASCADE,
    IsDeployed     INTEGER CONSTRAINT bool CHECK (IsDeployed IN (0, 1) ) 
                           DEFAULT (0) 
                           NOT NULL
);


-- Table: UrlRule
DROP TABLE IF EXISTS UrlRule;

CREATE TABLE IF NOT EXISTS UrlRule (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT    UNIQUE,
    URLRule TEXT    NOT NULL
);

INSERT INTO UrlRule (id, Name, URLRule) VALUES (1, 'Bethesda Creations', 'https://creations.bethesda.net/{lang}/{ModInfo.BethesdaKey}/details/{BethesdaID}');
INSERT INTO UrlRule (id, Name, URLRule) VALUES (2, 'Nexus', 'https://nexusmods.com/{Game.NexusKey}/mods/{ModInfo.NexusID}');

-- Index: ModItems_ModItems_ModItems_ModItems_idx_ModItems_CurrentStageID
DROP INDEX IF EXISTS ModItems_ModItems_ModItems_ModItems_idx_ModItems_CurrentStageID;

CREATE INDEX IF NOT EXISTS ModItems_ModItems_ModItems_ModItems_idx_ModItems_CurrentStageID ON ModItems (
    CurrentStageID ASC
);


-- Index: ModItems_ModItems_sqlite_autoindex_ModItems_1
DROP INDEX IF EXISTS ModItems_ModItems_sqlite_autoindex_ModItems_1;

CREATE UNIQUE INDEX IF NOT EXISTS ModItems_ModItems_sqlite_autoindex_ModItems_1 ON ModItems (
    ModName ASC
);


-- Index: ModItems_sqlite_autoindex_ModItems_1
DROP INDEX IF EXISTS ModItems_sqlite_autoindex_ModItems_1;

CREATE UNIQUE INDEX IF NOT EXISTS ModItems_sqlite_autoindex_ModItems_1 ON ModItems (
    ModName ASC
);


-- Index: Stages_sqlite_autoindex_Stages_1
DROP INDEX IF EXISTS Stages_sqlite_autoindex_Stages_1;

CREATE UNIQUE INDEX IF NOT EXISTS Stages_sqlite_autoindex_Stages_1 ON Stages (
    StageName ASC
);


-- Index: Stages_Stages_sqlite_autoindex_Stages_1
DROP INDEX IF EXISTS Stages_Stages_sqlite_autoindex_Stages_1;

CREATE UNIQUE INDEX IF NOT EXISTS Stages_Stages_sqlite_autoindex_Stages_1 ON Stages (
    StageName ASC
);


COMMIT TRANSACTION;
PRAGMA foreign_keys = on;
