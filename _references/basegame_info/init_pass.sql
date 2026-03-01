--
-- File generated with SQLiteStudio v3.4.21 on Sat Feb 28 21:23:21 2026
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

INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (1, 'Creation Kit', NULL, NULL, NULL, 0, 0);
INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (2, 'xEdit', NULL, NULL, NULL, 0, 0);
INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (3, 'NifSkope', NULL, NULL, NULL, 0, 0);
INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (4, 'Vortex', NULL, NULL, NULL, 0, 0);
INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (5, 'Archive2', NULL, NULL, NULL, 0, 0);
INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (6, 'Elric', NULL, NULL, NULL, 0, 0);
INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (7, 'Papyrus Compiler', NULL, NULL, NULL, 0, 0);
INSERT INTO ExternalTools (id, Name, baseFolderId, deployFolderId, Arguments, AdditionalArguments, CheckDeploy) VALUES (8, 'Asset Watcher', NULL, NULL, NULL, 0, 0);

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
    id                   INTEGER  PRIMARY KEY AUTOINCREMENT,
    Name                 TEXT     NOT NULL,
    DTStamp              DATETIME NOT NULL,
    Size                 NUMERIC  NOT NULL,
    StoreHashAlgorithm   TEXT     NULL,-- e.g. MD5/SHA1 from store manifest metadata
    StoreHash            BLOB     NULL,
    FastHashAlgorithm    TEXT     NULL,-- DMM fast/high-confidence comparator fingerprint scheme id
    FastHash             BLOB     NULL,
    FastHashMetadata     BLOB     NULL,-- Optional serialized fingerprint metadata (e.g. sampled block offsets/sizes)
    GameId               INTEGER  REFERENCES Game (id) ON DELETE CASCADE
                                                       ON UPDATE CASCADE,
    ArchiveFileId        INTEGER  REFERENCES FileInfo (id) ON DELETE SET NULL
                                                           ON UPDATE CASCADE,
    JunctionSourceId     INTEGER  REFERENCES FileInfo (id) ON DELETE SET NULL
                                                           ON UPDATE CASCADE,
    RelativeFolderId     INTEGER  NULL
                                  REFERENCES Folders (id) ON DELETE SET NULL
                                                          ON UPDATE CASCADE,
    FileTypeId           INTEGER  REFERENCES FileType (id) ON DELETE SET NULL
                                                           ON UPDATE CASCADE,
    ModId                INTEGER  NULL
                                  REFERENCES ModItems (id) ON DELETE SET NULL
                                                           ON UPDATE CASCADE,
    FileStorageKindId    INTEGER  NOT NULL
                                  DEFAULT (1) 
                                  REFERENCES FileStorageKind (id) ON DELETE RESTRICT
                                                                  ON UPDATE CASCADE,
    DeclaredByFileInfoId INTEGER  NULL
                                  REFERENCES FileInfo (id) ON DELETE SET NULL
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


-- Table: FileStorageKind
DROP TABLE IF EXISTS FileStorageKind;

CREATE TABLE IF NOT EXISTS FileStorageKind (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT    UNIQUE
                        NOT NULL,
    Description TEXT
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
    PlatformId    INTEGER REFERENCES Platform (id) ON DELETE CASCADE
                                                   ON UPDATE CASCADE,
    GameProfileId INTEGER REFERENCES GameProfile (id) ON DELETE CASCADE
                                                      ON UPDATE CASCADE,
    AssetKindId   INTEGER REFERENCES AssetKind (id) ON DELETE SET NULL
                                                    ON UPDATE CASCADE
);

INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (1, '.nif', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (2, '.mat', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (3, '.psc', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (4, '.pex', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (5, '.ini', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (6, '.exe', NULL, 0, 1, 0, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (7, '.ba2', NULL, 1, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (8, '.tif', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (9, '.dds', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (10, '.btd', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (11, '.btc', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (12, '.biom', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (13, '.bk2', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (14, '.wav', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (15, '.wem', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (16, '.ffxanim', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (17, '.psfx', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (18, '.mesh', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (19, '.esm', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (20, '.esp', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (21, '.esl', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (22, '.lip', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (23, '.fuz', NULL, 0, 1, 1, 0, NULL, NULL, NULL);
INSERT INTO FileType (id, FileExtension, Regex, IsArchive, ExcludeBA2, isModFile, ExcludeBackup, PlatformId, GameProfileId, AssetKindId) VALUES (24, '.sfs', NULL, 0, 1, 1, 0, NULL, NULL, NULL);

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

INSERT INTO FolderRole (id, Name, IncludeInBackup, PlatformId, IsRepoFolder, IsToolFolder) VALUES (1, 'GameInstall', 1, NULL, 0, 0);

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

INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (1, 'M:\Games\Fallout 4\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (2, 'M:\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (3, 'M:\Games\Fallout 4 (PC)\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (4, 'G:\SteamLibrary\steamapps\common\Fallout 4\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (5, 'G:\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (6, 'G:\SteamLibrary\steamapps\common\Skyrim Special Edition\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (7, 'G:\Games\Starfield\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (8, 'G:\SteamLibrary\steamapps\common\Starfield\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (9, 'G:\Games\Constellation\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (10, 'G:\Games\Old Mars\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (11, 'G:\Games\Shattered Space\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (12, 'M:\Games\Fallout 4- Automatron (PC)\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (13, 'M:\Games\Fallout 4- Automatron\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (14, 'M:\Games\Fallout 4- Contraptions Workshop (PC)\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (15, 'M:\Games\Fallout 4- Contraptions Workshop\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (16, 'M:\Games\Fallout 4- Far Harbor (PC)\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (17, 'M:\Games\Fallout 4- Far Harbor\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (18, 'M:\Games\Fallout 4- High Resolution Texture Pack\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (19, 'M:\Games\Fallout 4- Nuka-World (PC)\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (20, 'M:\Games\Fallout 4- Nuka-World\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (21, 'M:\Games\Fallout 4- Vault-Tec Workshop (PC)\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (22, 'M:\Games\Fallout 4- Vault-Tec Workshop\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (23, 'M:\Games\Fallout 4- Wasteland Workshop (PC)\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (24, 'M:\Games\Fallout 4- Wasteland Workshop\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (25, 'M:\SteamLibrary\steamapps\common\Fallout 4\', NULL, NULL, NULL, 1, 1);
INSERT INTO Folders (id, Path, Description, ParentFolderId, JunctionSourceId, FolderTypeId, FolderRoleId) VALUES (26, 'M:\SteamLibrary\steamapps\common\Skyrim Special Edition\', NULL, NULL, NULL, 1, 1);

-- Table: FolderType
DROP TABLE IF EXISTS FolderType;

CREATE TABLE IF NOT EXISTS FolderType (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT    UNIQUE
                 NOT NULL
);

INSERT INTO FolderType (id, Name) VALUES (1, 'GameInstall');

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
                                                                    ON UPDATE CASCADE,
    ParentGameId                   INTEGER NULL
                                           REFERENCES Game (id) ON DELETE SET NULL
                                                                ON UPDATE CASCADE,
    IsBaseGame                     INTEGER NOT NULL
                                           DEFAULT (1) 
                                           CHECK (IsBaseGame IN (0, 1) ),
    IsDlc                          INTEGER NOT NULL
                                           DEFAULT (0) 
                                           CHECK (IsDlc IN (0, 1) ) 
);

INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (1, 'Starfield', 'Starfield.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, 0);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (2, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, 0);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (3, 'Skyrim Special Edition', 'SkyrimSE.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, 0);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (4, 'Starfield - Shattered Space', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (5, 'Starfield - Old Mars', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (6, 'Starfield - Constellation', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 1, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (7, 'Fallout 4: Automatron', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (8, 'Fallout 4: Contraptions Workshop', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (9, 'Fallout 4: Far Harbor', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (10, 'Fallout 4: High Resolution Texture Pack', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (11, 'Fallout 4: Nuka-World', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (12, 'Fallout 4: Vault-Tec Workshop', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, 0, 1);
INSERT INTO Game (id, Name, Executable, NexusKeyword, BethesdaKeyword, GameSettingsFolderId, GameSaveFolderId, GameLocalSettingsFolderId, GamePluginsTxtId, GameSettingsIniId, GameCustomSettingsIniId, CreationKitSettingsIniId, CreationKitCustomSettingsIniId, ParentGameId, IsBaseGame, IsDlc) VALUES (13, 'Fallout 4: Wasteland Workshop', '', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 2, 0, 1);

-- Table: GameKnownPlugin
DROP TABLE IF EXISTS GameKnownPlugin;

CREATE TABLE IF NOT EXISTS GameKnownPlugin (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    GameId      INTEGER NOT NULL
                        REFERENCES Game (id) ON DELETE CASCADE
                                             ON UPDATE CASCADE,
    DisplayName TEXT    NOT NULL,
    PluginName  TEXT    NOT NULL,
    IsBaseGame  INTEGER NOT NULL
                        DEFAULT (0) 
                        CHECK (IsBaseGame IN (0, 1) ),
    IsDlc       INTEGER NOT NULL
                        DEFAULT (0) 
                        CHECK (IsDlc IN (0, 1) ),
    UNIQUE (
        GameId,
        PluginName
    )
);

INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (1, 1, 'Trackers Alliance support', 'SFBGS003.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (2, 1, 'Vehicle / REV-8', 'SFBGS004.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (3, 1, 'Ship Decoration', 'SFBGS006.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (4, 1, 'Gameplay Options', 'SFBGS007.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (5, 1, 'City Maps Data', 'SFBGS008.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (6, 2, 'Makeshift Weapon Pack - When Pigs Fly', 'ccSBJFO4003-Grenade.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (7, 2, 'Halloween Workshop Pack - All Hallows'' Eve', 'ccFSVFO4007-Halloween.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (8, 2, 'Enclave Remnants - Echoes of the Past', 'ccOTMFO4001-Remnants.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (9, 2, 'Tesla Cannon - Best of Three', 'ccBGSFO4046-TesCan.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (10, 2, 'Hellfire Power Armor - Pyromaniac', 'ccBGSFO4044-HellfirePowerArmor.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (11, 2, 'X-02 Power Armor - Speak of the Devil', 'ccBGSFO4115-X02.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (12, 2, 'Heavy Incinerator - Crucible', 'ccBGSFO4116-HeavyFlamer.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (13, 2, 'Enclave Armor Skins', 'ccBGSFO4096-AS_Enclave.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (14, 2, 'Enclave Weapon Skins', 'ccBGSFO4110-WS_Enclave.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (15, 3, 'Dawnguard', 'Dawnguard.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (16, 3, 'Hearthfire', 'HearthFires.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (17, 3, 'Dragonborn', 'Dragonborn.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (18, 3, 'Saints & Seducers', 'ccBGSSSE025-AdvDSGS.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (19, 3, 'Rare Curios', 'ccBGSSSE037-Curios.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (20, 3, 'Survival Mode', 'ccQDRSSE001-SurvivalMode.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (21, 3, 'Fishing', 'ccBGSSSE001-Fish.esm', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (22, 3, 'Resource Pack', '_ResourcePack.esl', 1, 0);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (23, 4, 'Shattered Space', 'ShatteredSpace.esm', 0, 1);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (24, 7, 'Automatron', 'DLCRobot.esm', 0, 1);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (25, 8, 'Contraptions Workshop', 'DLCworkshop02.esm', 0, 1);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (26, 9, 'Far Harbor', 'DLCCoast.esm', 0, 1);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (27, 11, 'Nuka-World', 'DLCNukaWorld.esm', 0, 1);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (28, 12, 'Vault-Tec Workshop', 'DLCworkshop03.esm', 0, 1);
INSERT INTO GameKnownPlugin (id, GameId, DisplayName, PluginName, IsBaseGame, IsDlc) VALUES (29, 13, 'Wasteland Workshop', 'DLCworkshop01.esm', 0, 1);

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
    UNIQUE (
        Name
    )
);

INSERT INTO GameSource (id, Name, SourceGameId, URL, URI) VALUES (1, 'Steam', NULL, 'https://store.steampowered.com/', 'steam://');
INSERT INTO GameSource (id, Name, SourceGameId, URL, URI) VALUES (2, 'GamePass', NULL, 'https://www.xbox.com/xbox-game-pass', 'ms-xbox-gamepass://');
INSERT INTO GameSource (id, Name, SourceGameId, URL, URI) VALUES (3, 'Epic', NULL, 'https://store.epicgames.com/', 'com.epicgames.launcher://');
INSERT INTO GameSource (id, Name, SourceGameId, URL, URI) VALUES (4, 'GoG', NULL, 'https://www.gog.com/', 'goggalaxy://');
INSERT INTO GameSource (id, Name, SourceGameId, URL, URI) VALUES (5, 'Starfield-Steam', '1716740', 'https://store.steampowered.com/app/1716740', 'steam://run/1716740');
INSERT INTO GameSource (id, Name, SourceGameId, URL, URI) VALUES (6, 'Fallout4-Steam', '377160', 'https://store.steampowered.com/app/377160', 'steam://run/377160');
INSERT INTO GameSource (id, Name, SourceGameId, URL, URI) VALUES (7, 'Skyrim-Steam', '489830', 'https://store.steampowered.com/app/489830', 'steam://run/489830');

-- Table: GameStoreApp
DROP TABLE IF EXISTS GameStoreApp;

CREATE TABLE IF NOT EXISTS GameStoreApp (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    GameId       INTEGER NOT NULL
                         REFERENCES Game (id) ON DELETE CASCADE
                                              ON UPDATE CASCADE,
    GameSourceId INTEGER NOT NULL
                         REFERENCES GameSource (id) ON DELETE CASCADE
                                                    ON UPDATE CASCADE,
    StoreAppId   TEXT    NOT NULL,
    UNIQUE (
        GameSourceId,
        StoreAppId
    ),
    UNIQUE (
        GameId,
        GameSourceId,
        StoreAppId
    )
);

INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (1, 1, 1, '1716740');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (2, 2, 1, '377160');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (3, 3, 1, '489830');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (4, 1, 2, 'BethesdaSoftworks.ProjectGold');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (5, 4, 2, 'BethesdaSoftworks.ShatteredSpace');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (6, 5, 2, 'BethesdaSoftworks.PGPreorderContentwPkg');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (7, 6, 2, 'BethesdaSoftworks.PGDeluxeContentwPkg');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (8, 2, 2, 'BethesdaSoftworks.Fallout4-CoreGame');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (9, 7, 2, 'BethesdaSoftworks.Fallout4-DLC1Automatron');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (10, 8, 2, 'BethesdaSoftworks.Fallout4-DLC4ContraptionsWorksho');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (11, 9, 2, 'BethesdaSoftworks.Fallout4-DLC3FarHarbor');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (12, 10, 2, 'BethesdaSoftworks.Fallout4HighResolutionTexturePac');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (13, 11, 2, 'BethesdaSoftworks.Fallout4-DLC6Nuka-World');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (14, 12, 2, 'BethesdaSoftworks.Fallout4-DLC5Vault-TecWorkshop');
INSERT INTO GameStoreApp (id, GameId, GameSourceId, StoreAppId) VALUES (15, 13, 2, 'BethesdaSoftworks.Fallout4-DLC2WastelandWorkshop');

-- Table: GameStoreInstall
DROP TABLE IF EXISTS GameStoreInstall;

CREATE TABLE IF NOT EXISTS GameStoreInstall (
    id                INTEGER  PRIMARY KEY AUTOINCREMENT,
    GameStoreRootId   INTEGER  NOT NULL
                               REFERENCES GameStoreRoot (id) ON DELETE CASCADE
                                                             ON UPDATE CASCADE,
    InstallFolderId   INTEGER  NOT NULL
                               REFERENCES Folders (id) ON DELETE CASCADE
                                                       ON UPDATE CASCADE,
    ContentFolderId   INTEGER  NULL
                               REFERENCES Folders (id) ON DELETE SET NULL
                                                       ON UPDATE CASCADE,
    DataFolderId      INTEGER  NULL
                               REFERENCES Folders (id) ON DELETE SET NULL
                                                       ON UPDATE CASCADE,
    ManifestFileId    INTEGER  NULL
                               REFERENCES FileInfo (id) ON DELETE SET NULL
                                                        ON UPDATE CASCADE,
    GameId            INTEGER  NULL
                               REFERENCES Game (id) ON DELETE SET NULL
                                                    ON UPDATE CASCADE,
    StoreAppId        TEXT     NOT NULL,-- Steam AppId / Xbox ProductId / etc.
    InstallInstanceId TEXT     NULL,-- EGS GUID / .item filename / etc.
    DisplayName       TEXT     NULL,
    ExecutableName    TEXT     NULL,
    Version           TEXT     NULL,
    IconFileId        INTEGER  NULL
                               REFERENCES FileInfo (id) ON DELETE SET NULL
                                                        ON UPDATE CASCADE,
    LogoFileId        INTEGER  NULL
                               REFERENCES FileInfo (id) ON DELETE SET NULL
                                                        ON UPDATE CASCADE,
    SplashFileId      INTEGER  NULL
                               REFERENCES FileInfo (id) ON DELETE SET NULL
                                                        ON UPDATE CASCADE,
    IdentityName      TEXT     NULL,
    TitleId           TEXT     NULL,
    ProductId         TEXT     NULL,
    ContentIdOverride TEXT     NULL,
    LastSeenDT        DATETIME NOT NULL,
    UNIQUE (
        GameStoreRootId,
        StoreAppId,
        InstallFolderId
    ),
    UNIQUE (
        GameStoreRootId,
        InstallInstanceId
    )
);

INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (1, 1, 1, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0011978+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (2, 1, 3, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0013050+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (3, 2, 4, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0014014+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (4, 2, 6, NULL, NULL, NULL, 3, '489830', NULL, 'Skyrim Special Edition', 'SkyrimSE.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0014539+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (5, 3, 7, NULL, NULL, NULL, 1, '1716740', NULL, 'Starfield', 'Starfield.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0015086+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (6, 2, 8, NULL, NULL, NULL, 1, '1716740', NULL, 'Starfield', 'Starfield.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0015487+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (7, 3, 9, NULL, NULL, NULL, 1, '1716740', NULL, 'Starfield', 'Starfield.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0015869+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (8, 3, 10, NULL, NULL, NULL, 1, '1716740', NULL, 'Starfield', 'Starfield.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0017394+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (9, 3, 11, NULL, NULL, NULL, 1, '1716740', NULL, 'Starfield', 'Starfield.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0017873+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (10, 1, 12, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0018347+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (11, 1, 13, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0018801+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (12, 1, 14, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0020785+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (13, 1, 15, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0021752+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (14, 1, 16, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0022476+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (15, 1, 17, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0023190+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (16, 1, 18, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0023830+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (17, 1, 19, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0025314+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (18, 1, 20, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0025930+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (19, 1, 21, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0026516+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (20, 1, 22, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0027092+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (21, 1, 23, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0027663+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (22, 1, 24, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0028236+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (23, 4, 25, NULL, NULL, NULL, 2, '377160', NULL, 'Fallout 4', 'Fallout4.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0028951+00:00');
INSERT INTO GameStoreInstall (id, GameStoreRootId, InstallFolderId, ContentFolderId, DataFolderId, ManifestFileId, GameId, StoreAppId, InstallInstanceId, DisplayName, ExecutableName, Version, IconFileId, LogoFileId, SplashFileId, IdentityName, TitleId, ProductId, ContentIdOverride, LastSeenDT) VALUES (24, 4, 26, NULL, NULL, NULL, 3, '489830', NULL, 'Skyrim Special Edition', 'SkyrimSE.exe', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-03-01T03:19:26.0029450+00:00');

-- Table: GameStoreInstallDepot
DROP TABLE IF EXISTS GameStoreInstallDepot;

CREATE TABLE IF NOT EXISTS GameStoreInstallDepot (
    id          INTEGER  PRIMARY KEY AUTOINCREMENT,
    InstallId   INTEGER  NOT NULL
                         REFERENCES GameStoreInstall (id) ON DELETE CASCADE
                                                          ON UPDATE CASCADE,
    DepotId     TEXT     NOT NULL,
    ManifestId  TEXT     NULL,
    BranchName  TEXT     NULL,-- Normalize NULLs for uniqueness (SQLite allows expressions here)
    ManifestKey TEXT     GENERATED ALWAYS AS (IFNULL(ManifestId, '') ) STORED,
    BranchKey   TEXT     GENERATED ALWAYS AS (IFNULL(BranchName, '') ) STORED,
    IsDlcDepot  INTEGER  NOT NULL
                         DEFAULT 0
                         CHECK (IsDlcDepot IN (0, 1) ),
    LastSeenDT  DATETIME NOT NULL,
    UNIQUE (
        InstallId,
        DepotId,
        ManifestKey,
        BranchKey
    )
);


-- Table: GameStoreInstallFile
DROP TABLE IF EXISTS GameStoreInstallFile;

CREATE TABLE IF NOT EXISTS GameStoreInstallFile (
    id                  INTEGER  PRIMARY KEY AUTOINCREMENT,
    InstallId           INTEGER  NOT NULL
                                 REFERENCES GameStoreInstall (id) ON DELETE CASCADE
                                                                  ON UPDATE CASCADE,
    FileInfoId          INTEGER  NOT NULL
                                 REFERENCES FileInfo (id) ON DELETE CASCADE
                                                          ON UPDATE CASCADE,
    RelativePath        TEXT     NULL,
    RelativePathKey     TEXT     GENERATED ALWAYS AS (IFNULL(RelativePath, '') ) STORED,
    FileRole            TEXT     NOT NULL
                                 CHECK (FileRole IN ('Actual', 'Reference') ),
    IsPresentOnDisk     INTEGER  NOT NULL
                                 DEFAULT (1) 
                                 CHECK (IsPresentOnDisk IN (0, 1) ),
    QuickCheckAlgorithm TEXT     NULL,
    QuickCheckSignature BLOB     NULL,
    LastValidatedDT     DATETIME NULL,
    UNIQUE (
        InstallId,
        FileInfoId,
        FileRole,
        RelativePathKey
    )
);


-- Table: GameStoreProductLink
DROP TABLE IF EXISTS GameStoreProductLink;

CREATE TABLE IF NOT EXISTS GameStoreProductLink (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    ChildInstallId     INTEGER NOT NULL
                               REFERENCES GameStoreInstall (id) ON DELETE CASCADE
                                                                ON UPDATE CASCADE,
    ParentGameSourceId INTEGER NOT NULL
                               REFERENCES GameSource (id) ON DELETE CASCADE
                                                          ON UPDATE CASCADE,
    ParentStoreAppId   TEXT    NOT NULL,-- store id of base product
    LinkType           TEXT    NOT NULL,-- e.g. 'AllowedProduct', 'DLC', etc.
    UNIQUE (
        ChildInstallId,
        ParentGameSourceId,
        ParentStoreAppId,
        LinkType
    )
);

INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (1, 7, 2, '1716740', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (2, 8, 2, '1716740', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (3, 9, 2, '1716740', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (4, 10, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (5, 11, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (6, 12, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (7, 13, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (8, 14, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (9, 15, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (10, 16, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (11, 17, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (12, 18, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (13, 19, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (14, 20, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (15, 21, 2, '377160', 'DLC');
INSERT INTO GameStoreProductLink (id, ChildInstallId, ParentGameSourceId, ParentStoreAppId, LinkType) VALUES (16, 22, 2, '377160', 'DLC');

-- Table: GameStoreRoot
DROP TABLE IF EXISTS GameStoreRoot;

CREATE TABLE IF NOT EXISTS GameStoreRoot (
    id           INTEGER  PRIMARY KEY AUTOINCREMENT,
    GameSourceId INTEGER  NOT NULL
                          REFERENCES GameSource (id) ON DELETE CASCADE
                                                     ON UPDATE CASCADE,
    RootFolderId INTEGER  NOT NULL
                          REFERENCES Folders (id) ON DELETE CASCADE
                                                  ON UPDATE CASCADE,
    RootType     TEXT     NOT NULL,
    LastSeenDT   DATETIME NOT NULL,
    UNIQUE (
        GameSourceId,
        RootFolderId,
        RootType
    )
);

INSERT INTO GameStoreRoot (id, GameSourceId, RootFolderId, RootType, LastSeenDT) VALUES (1, 2, 2, 'Library', '2026-03-01T03:19:26.0011334+00:00');
INSERT INTO GameStoreRoot (id, GameSourceId, RootFolderId, RootType, LastSeenDT) VALUES (2, 1, 5, 'Library', '2026-03-01T03:19:26.0013738+00:00');
INSERT INTO GameStoreRoot (id, GameSourceId, RootFolderId, RootType, LastSeenDT) VALUES (3, 2, 5, 'Library', '2026-03-01T03:19:26.0014976+00:00');
INSERT INTO GameStoreRoot (id, GameSourceId, RootFolderId, RootType, LastSeenDT) VALUES (4, 1, 2, 'Library', '2026-03-01T03:19:26.0028796+00:00');

-- Table: GameVersion
DROP TABLE IF EXISTS GameVersion;

CREATE TABLE IF NOT EXISTS GameVersion (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Version     TEXT,
    GameId      INTEGER REFERENCES Game (id) ON DELETE CASCADE
                                             ON UPDATE CASCADE,-- Keep for now (legacy / optional); but don?t rely on it for installs.
    GameSource  INTEGER REFERENCES GameSource (id) ON DELETE SET NULL
                                                   ON UPDATE CASCADE,
    VersionRepo TEXT-- optional: repo/tag/commit reference for a pinned toolchain/version
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


-- Table: GameVersionSteamDepot
DROP TABLE IF EXISTS GameVersionSteamDepot;

CREATE TABLE IF NOT EXISTS GameVersionSteamDepot (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    GameVersionId INTEGER NOT NULL
                          REFERENCES GameVersion (id) ON DELETE CASCADE
                                                      ON UPDATE CASCADE,
    AppId         TEXT    NOT NULL,
    DepotId       TEXT    NOT NULL,
    ManifestId    TEXT    NOT NULL,
    UNIQUE (
        GameVersionId,
        AppId,
        DepotId,
        ManifestId
    )
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

INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (1, 'PC-Steam', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (2, 'PC-Gamepass', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (3, 'PC-Epic', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (4, 'PC-GoG', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (5, 'XBox', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (6, 'PSN', NULL);
INSERT INTO Platform (id, Name, ArchiveNamePattern) VALUES (7, 'Switch', NULL);

-- Table: ReservedFolderNames
DROP TABLE IF EXISTS ReservedFolderNames;

CREATE TABLE IF NOT EXISTS ReservedFolderNames (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT    UNIQUE
                 NOT NULL
);


-- Table: SeedHistory
DROP TABLE IF EXISTS SeedHistory;

CREATE TABLE IF NOT EXISTS SeedHistory (
    Version   TEXT PRIMARY KEY,
    AppliedAt TEXT NOT NULL
);

INSERT INTO SeedHistory (Version, AppliedAt) VALUES ('2026.02-baseline-core', '2026-03-01T03:15:40.8087095+00:00');

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

INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (1, 'DEV', 1, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (2, 'TEST', 0, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (3, 'PREFLIGHT', 0, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (4, 'PROD', 0, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (5, 'NEXUS', 0, 1);
INSERT INTO Stages (id, StageName, IsSource, IsReserved) VALUES (6, 'CREATIONS', 0, 1);

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

INSERT INTO UrlRule (id, Name, URLRule) VALUES (1, 'Steam-App', 'https://store.steampowered.com/app/{id}');
INSERT INTO UrlRule (id, Name, URLRule) VALUES (2, 'GamePass-Product', 'https://www.xbox.com/games/store/{slug}');
INSERT INTO UrlRule (id, Name, URLRule) VALUES (3, 'Epic-Product', 'https://store.epicgames.com/p/{slug}');
INSERT INTO UrlRule (id, Name, URLRule) VALUES (4, 'GoG-Product', 'https://www.gog.com/en/game/{slug}');

COMMIT TRANSACTION;
PRAGMA foreign_keys = on;
