--
-- File generated with SQLiteStudio v3.4.17 on Thu Dec 4 13:55:23 2025
--
-- Text encoding used: System
--
PRAGMA foreign_keys = off;
BEGIN TRANSACTION;

-- Table: Config
DROP TABLE IF EXISTS Config;

CREATE TABLE Config (
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
    DefaultGameProfile    INTEGER REFERENCES Game (id) ON DELETE SET NULL
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

CREATE TABLE ExternalIds (
    id         INTEGER PRIMARY KEY AUTOINCREMENT
                       NOT NULL,
    modId      INTEGER REFERENCES ModItems (id) ON DELETE CASCADE
                                                ON UPDATE CASCADE,
    BethesdaId TEXT    NULL
                       UNIQUE,
    NexusId    TEXT    NULL
                       UNIQUE
);


-- Table: ExternalTools
DROP TABLE IF EXISTS ExternalTools;

CREATE TABLE ExternalTools (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT
                                UNIQUE
                                NOT NULL,
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

CREATE TABLE FileDependency (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT
                                NOT NULL,
    FileInfoId          INTEGER REFERENCES FileInfo (id) ON DELETE CASCADE
                                                         ON UPDATE CASCADE,
    DependentFileInfoId INTEGER REFERENCES FileInfo (id) ON DELETE CASCADE
                                                         ON UPDATE CASCADE
);


-- Table: FileFolderDeploymentState
DROP TABLE IF EXISTS FileFolderDeploymentState;

CREATE TABLE FileFolderDeploymentState (
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

CREATE TABLE FileInfo (
    id               INTEGER  PRIMARY KEY AUTOINCREMENT
                              UNIQUE
                              NOT NULL,
    Name             TEXT     NOT NULL,
    DTStamp          DATETIME NOT NULL,
    Size             NUMERIC  NOT NULL,
    Hash             BLOB     NULL,
    FileTypeId       INTEGER  REFERENCES FileType (id) ON DELETE SET NULL
                                                       ON UPDATE CASCADE,
    RelativeFolderId INTEGER  NULL
                              REFERENCES Folders (id) ON DELETE SET NULL
                                                      ON UPDATE CASCADE,
    ModId            INTEGER  NULL
                              REFERENCES ModItems (ModId) ON DELETE SET NULL
                                                          ON UPDATE CASCADE
);


-- Table: FileStage
DROP TABLE IF EXISTS FileStage;

CREATE TABLE FileStage (
    id         INTEGER PRIMARY KEY AUTOINCREMENT
                       NOT NULL,
    FileInfoId INTEGER REFERENCES FileInfo (id) ON DELETE CASCADE
                                                ON UPDATE CASCADE,
    StageId    INTEGER REFERENCES Stages (id) ON DELETE CASCADE
                                              ON UPDATE CASCADE
);


-- Table: FileType
DROP TABLE IF EXISTS FileType;

CREATE TABLE FileType (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    FileExtension     TEXT    NOT NULL,
    IsArchive         INTEGER CONSTRAINT bool CHECK (IsArchive IN (0, 1) ) 
                              DEFAULT (0),
    ExcludeBA2        INTEGER CONSTRAINT bool CHECK (ExcludeBA2 IN (0, 1) ) 
                              DEFAULT (1),
    IsXBoxSpecific    INTEGER CONSTRAINT bool CHECK (IsXBoxSpecific IN (0, 1) ) 
                              DEFAULT (0),
    IsProfileSpecific INTEGER CONSTRAINT bool CHECK (IsProfileSpecific IN (0, 1) ) 
                              DEFAULT (0) 
);


-- Table: Folders
DROP TABLE IF EXISTS Folders;

CREATE TABLE Folders (
    id           INTEGER PRIMARY KEY AUTOINCREMENT
                         UNIQUE
                         NOT NULL,
    Path         TEXT    UNIQUE
                         NOT NULL,
    Description  TEXT,
    FolderTypeId INTEGER NOT NULL
                         REFERENCES FolderType (id) 
);


-- Table: FolderType
DROP TABLE IF EXISTS FolderType;

CREATE TABLE FolderType (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT
                                UNIQUE
                                NOT NULL,
    Name                TEXT    UNIQUE
                                NOT NULL,
    IsRelative          INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsRelative IN (0, 1) ) 
                                DEFAULT (0),
    ParentFolderTypeId  INTEGER NULL,
    IsGameFolder        INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsGameFolder IN (0, 1) ) 
                                DEFAULT (0),
    IsToolFolder        INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsToolFolder IN (0, 1) ) 
                                DEFAULT (0),
    IsModFolder         INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsModFolder IN (0, 1) ) 
                                DEFAULT (0),
    IsBackupFolder      INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsBackupFolder IN (0, 1) ) 
                                DEFAULT (0),
    IsModManagerRepo    INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsModManagerRepo IN (0, 1) ) 
                                DEFAULT (0),
    IsGitRepo           INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsGitRepo IN (0, 1) ) 
                                DEFAULT (0),
    IsGitSubmodule      INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsGitSubmodule IN (0, 1) ) 
                                DEFAULT (0),
    IsJunction          INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsJunction IN (0, 1) ) 
                                DEFAULT (0),
    IsEnvironmentFolder INTEGER NOT NULL
                                CONSTRAINT bool CHECK (IsEnvironmentFolder IN (0, 1) ) 
                                DEFAULT (0) 
);


-- Table: Game
DROP TABLE IF EXISTS Game;

CREATE TABLE Game (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Name            TEXT    NOT NULL,
    Executable      TEXT,
    NexusKeyword    TEXT,
    BethesdaKeyword TEXT
);


-- Table: InitializationStatus
DROP TABLE IF EXISTS InitializationStatus;

CREATE TABLE InitializationStatus (
    Id                 BIGINT NOT NULL,
    IsInitialized      BIGINT NOT NULL,
    InitializationTime TEXT   NOT NULL,
    CONSTRAINT sqlite_autoindex_InitializationStatus_1 PRIMARY KEY (
        Id
    )
);


-- Table: ModItems
DROP TABLE IF EXISTS ModItems;

CREATE TABLE ModItems (
    ModID          INTEGER PRIMARY KEY AUTOINCREMENT
                           NOT NULL,
    ModName        TEXT    NOT NULL,
    ModFolderPath  TEXT    NOT NULL,
    CurrentStageID BIGINT  NULL,
    CONSTRAINT FK_ModItems_0_0 FOREIGN KEY (
        CurrentStageID
    )
    REFERENCES Stages (StageID) ON DELETE SET NULL
                                ON UPDATE NO ACTION
);


-- Table: ModStages
DROP TABLE IF EXISTS ModStages;

CREATE TABLE ModStages (
    [mod.id] BIGINT NOT NULL,
    StageID  BIGINT NOT NULL,
    CONSTRAINT sqlite_autoindex_ModStages_1 PRIMARY KEY (
        [mod.id],
        StageID
    ),
    CONSTRAINT FK_ModStages_0_0 FOREIGN KEY (
        StageID
    )
    REFERENCES Stages (StageID) ON DELETE NO ACTION
                                ON UPDATE NO ACTION,
    CONSTRAINT FK_ModStages_1_0 FOREIGN KEY (
        [mod.id]
    )
    REFERENCES ModItem (ModID) ON DELETE NO ACTION
                               ON UPDATE NO ACTION
);


-- Table: Stages
DROP TABLE IF EXISTS Stages;

CREATE TABLE Stages (
    StageID    INTEGER PRIMARY KEY AUTOINCREMENT
                       NOT NULL,
    StageName  TEXT    NOT NULL,
    IsSource   BIGINT  DEFAULT (0) 
                       NULL,
    IsReserved BIGINT  DEFAULT (0) 
                       NULL
);


-- Table: ToolDeploymentState
DROP TABLE IF EXISTS ToolDeploymentState;

CREATE TABLE ToolDeploymentState (
    id             INTEGER PRIMARY KEY AUTOINCREMENT
                           NOT NULL,
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

CREATE TABLE UrlRule (
    id      INTEGER PRIMARY KEY AUTOINCREMENT
                    NOT NULL,
    Name    TEXT    UNIQUE,
    URLRule TEXT    NOT NULL
);


COMMIT TRANSACTION;
PRAGMA foreign_keys = on;
