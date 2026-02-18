--
-- File generated with SQLiteStudio v3.4.21 on Tue Feb 17 20:00:57 2026
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
    GameId           INTEGER  REFERENCES Game (id) ON DELETE CASCADE
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
    PlatformId    INTEGER REFERENCES Platform (id) ON DELETE CASCADE
                                                   ON UPDATE CASCADE,
    GameProfileId INTEGER REFERENCES GameProfile (id) ON DELETE CASCADE
                                                      ON UPDATE CASCADE,
    AssetKindId   INTEGER REFERENCES AssetKind (id) ON DELETE SET NULL
                                                    ON UPDATE CASCADE
);


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
    StoreAppId        TEXT     NOT NULL,
    IdentityName      TEXT     NULL,
    TitleId           TEXT     NULL,
    ProductId         TEXT     NULL,
    ContentIdOverride TEXT     NULL,
    DisplayName       TEXT     NULL,
    ExecutableName    TEXT     NULL,
    Version           TEXT     NULL,
    LastSeenDT        DATETIME NOT NULL,
    UNIQUE (
        GameStoreRootId,
        StoreAppId,
        InstallFolderId
    )
);


-- Table: GameStoreProductLink
DROP TABLE IF EXISTS GameStoreProductLink;

CREATE TABLE IF NOT EXISTS GameStoreProductLink (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    ChildInstallId   INTEGER NOT NULL
                             REFERENCES GameStoreInstall (id) ON DELETE CASCADE
                                                              ON UPDATE CASCADE,
    ParentStoreAppId TEXT    NOT NULL,-- StoreId of base product (e.g. 9NCJSXWZTP88)
    LinkType         TEXT    NOT NULL,-- e.g. 'AllowedProduct'
    UNIQUE (
        ChildInstallId,
        ParentStoreAppId,
        LinkType
    )
);


-- Table: GameStoreRoot
DROP TABLE IF EXISTS GameStoreRoot;

CREATE TABLE IF NOT EXISTS GameStoreRoot (
    id              INTEGER  PRIMARY KEY AUTOINCREMENT,
    GameSourceId    INTEGER  NOT NULL
                             REFERENCES GameSource (id) ON DELETE CASCADE
                                                        ON UPDATE CASCADE,
    RootFolderId    INTEGER  NOT NULL
                             REFERENCES Folders (id) ON DELETE CASCADE
                                                     ON UPDATE CASCADE,
    DiscoverySource TEXT     NULL,
    DiscoveryRef    TEXT     NULL,
    DriveRoot       TEXT     NULL,
    LastSeenDT      DATETIME NOT NULL,
    UNIQUE (
        GameSourceId,
        RootFolderId
    )
);


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


COMMIT TRANSACTION;
PRAGMA foreign_keys = on;
