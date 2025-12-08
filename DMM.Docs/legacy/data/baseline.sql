--
-- File generated with SQLiteStudio v3.4.4 on Mon Oct 21 11:39:42 2024
--
-- Text encoding used: System
--
PRAGMA foreign_keys = off;
BEGIN TRANSACTION;

-- Table: ArchiveFormats
DROP TABLE IF EXISTS ArchiveFormats;

CREATE TABLE IF NOT EXISTS ArchiveFormats (
    ArchiveFormatID INTEGER PRIMARY KEY AUTOINCREMENT
                            NOT NULL,
    FormatName      TEXT    NOT NULL
                            UNIQUE
);

INSERT INTO ArchiveFormats (ArchiveFormatID, FormatName) VALUES (1, 'zip');
INSERT INTO ArchiveFormats (ArchiveFormatID, FormatName) VALUES (2, '7z');

-- Table: Config
DROP TABLE IF EXISTS Config;

CREATE TABLE IF NOT EXISTS Config (
    RepoFolder              TEXT   NOT NULL,
    UseGit                  BIGINT DEFAULT (1),
    GitHubRepo              TEXT,
    UseModManager           BIGINT DEFAULT (1),
    GameFolder              TEXT,
    ModStagingFolder        TEXT,
    ModManagerExecutable    TEXT,
    ModManagerParameters    TEXT,
    IDEExecutable           TEXT,
    LimitFileTypes          BIGINT DEFAULT (1),
    PromoteIncludeFiletypes TEXT,
    PackageExcludeFiletypes TEXT,
    ArchiveFormatID         BIGINT,
    TimestampFormat         TEXT,
    MyNameSpace             TEXT,
    MyResourcePrefix        TEXT,
    ShowSaveMessage         BIGINT DEFAULT (0),
    ShowOverwriteMessage    BIGINT DEFAULT (0),
    NexusAPIKey             TEXT,
    AutoCheckForUpdates     BIGINT DEFAULT (1),
    CONSTRAINT FK_Config_0_0 FOREIGN KEY (
        ArchiveFormatID
    )
    REFERENCES ArchiveFormats (ArchiveFormatID) ON DELETE SET NULL
                                                ON UPDATE NO ACTION
);


-- Table: ExternalIDs
DROP TABLE IF EXISTS ExternalIDs;

CREATE TABLE IF NOT EXISTS ExternalIDs (
    ExternalID INTEGER PRIMARY KEY AUTOINCREMENT
                       NOT NULL,
    PluginID   BIGINT,
    ModID      BIGINT,
    BethesdaID TEXT
                       UNIQUE,
    NexusID    TEXT
                       UNIQUE,
    CONSTRAINT FK_ExternalIDs_0_0 FOREIGN KEY (
        ModID
    )
    REFERENCES ModItems (ModID) ON DELETE NO ACTION
                                ON UPDATE NO ACTION,
    CONSTRAINT FK_ExternalIDs_1_0 FOREIGN KEY (
        PluginID
    )
    REFERENCES Plugins (PluginID) ON DELETE NO ACTION
                                  ON UPDATE NO ACTION
);


-- Table: FileInfo
DROP TABLE IF EXISTS FileInfo;

CREATE TABLE IF NOT EXISTS FileInfo (
    FileID       INTEGER  PRIMARY KEY AUTOINCREMENT
                          NOT NULL,
    PluginID     BIGINT,
    ModID        BIGINT,
    StageID      BIGINT,
    Filename     TEXT     NOT NULL,
    RelativePath TEXT,
    DTStamp      DATETIME NOT NULL,
    HASH         TEXT,
    IsArchive    BIGINT   NOT NULL,
    CONSTRAINT FK_FileInfo_0_0 FOREIGN KEY (
        ModID
    )
    REFERENCES ModItems (ModID) ON DELETE CASCADE
                                ON UPDATE NO ACTION,
    CONSTRAINT FK_FileInfo_1_0 FOREIGN KEY (
        StageID
    )
    REFERENCES Stages (StageID) ON DELETE NO ACTION
                                ON UPDATE NO ACTION,
    CONSTRAINT FK_FileInfo_2_0 FOREIGN KEY (
        PluginID
    )
    REFERENCES Plugins (PluginID) ON DELETE NO ACTION
                                  ON UPDATE NO ACTION
);


-- Table: InitializationStatus
DROP TABLE IF EXISTS InitializationStatus;

CREATE TABLE IF NOT EXISTS InitializationStatus (
    Id                 BIGINT NOT NULL,
    IsInitialized      BIGINT NOT NULL,
    InitializationTime TEXT   NOT NULL,
    CONSTRAINT sqlite_autoindex_InitializationStatus_1 PRIMARY KEY (
        Id
    )
);


-- Table: LoadOutProfile
DROP TABLE IF EXISTS LoadOutProfile;

CREATE TABLE IF NOT EXISTS LoadOutProfile (
    ProfileID   INTEGER PRIMARY KEY AUTOINCREMENT
                        NOT NULL,
    ProfileName TEXT    NOT NULL
                        UNIQUE
);


-- Table: ModGroups
DROP TABLE IF EXISTS ModGroups;

CREATE TABLE IF NOT EXISTS ModGroups (
    GroupID     INTEGER PRIMARY KEY AUTOINCREMENT
                        NOT NULL,
    Ordinal     INTEGER,
    GroupName   TEXT
                        UNIQUE,
    Description TEXT,
    ParentID    INTEGER,
    CONSTRAINT FK_ModGroups_0_0 FOREIGN KEY (
        ParentID
    )
    REFERENCES ModGroups (GroupID) ON DELETE SET NULL
                                   ON UPDATE NO ACTION
);


-- Table: ModItems
DROP TABLE IF EXISTS ModItems;

CREATE TABLE IF NOT EXISTS ModItems (
    ModID          INTEGER PRIMARY KEY AUTOINCREMENT
                           NOT NULL,
    ModName        TEXT    NOT NULL,
    ModFolderPath  TEXT    NOT NULL,
    CurrentStageID BIGINT,
    CONSTRAINT FK_ModItems_0_0 FOREIGN KEY (
        CurrentStageID
    )
    REFERENCES Stages (StageID) ON DELETE SET NULL
                                ON UPDATE NO ACTION
);


-- Table: ModStages
DROP TABLE IF EXISTS ModStages;

CREATE TABLE IF NOT EXISTS ModStages (
    ModID   BIGINT NOT NULL,
    StageID BIGINT NOT NULL,
    CONSTRAINT sqlite_autoindex_ModStages_1 PRIMARY KEY (
        ModID,
        StageID
    ),
    CONSTRAINT FK_ModStages_0_0 FOREIGN KEY (
        StageID
    )
    REFERENCES Stages (StageID) ON DELETE NO ACTION
                                ON UPDATE NO ACTION,
    CONSTRAINT FK_ModStages_1_0 FOREIGN KEY (
        ModID
    )
    REFERENCES ModItem (ModID) ON DELETE NO ACTION
                               ON UPDATE NO ACTION
);


-- Table: Plugins
DROP TABLE IF EXISTS Plugins;

CREATE TABLE IF NOT EXISTS Plugins (
    PluginID     INTEGER PRIMARY KEY AUTOINCREMENT
                         NOT NULL,
    PluginName   TEXT
                         UNIQUE,
    Description  TEXT,
    Achievements TEXT,
    DTStamp      TEXT    NOT NULL,
    Version      TEXT,
    GroupID      BIGINT,
    GroupOrdinal BIGINT,
    CONSTRAINT FK_Plugins_0_0 FOREIGN KEY (
        GroupID
    )
    REFERENCES ModGroups (GroupID) ON DELETE SET NULL
                                   ON UPDATE NO ACTION
);


-- Table: ProfilePlugins
DROP TABLE IF EXISTS ProfilePlugins;

CREATE TABLE IF NOT EXISTS ProfilePlugins (
    ProfileID BIGINT NOT NULL,
    PluginID  BIGINT NOT NULL,
    CONSTRAINT sqlite_autoindex_ProfilePlugins_1 PRIMARY KEY (
        ProfileID,
        PluginID
    ),
    CONSTRAINT FK_ProfilePlugins_0_0 FOREIGN KEY (
        PluginID
    )
    REFERENCES Plugins (PluginID) ON DELETE CASCADE
                                  ON UPDATE NO ACTION,
    CONSTRAINT FK_ProfilePlugins_1_0 FOREIGN KEY (
        ProfileID
    )
    REFERENCES LoadOutProfile (ProfileID) ON DELETE CASCADE
                                          ON UPDATE NO ACTION
);


-- Table: Stages
DROP TABLE IF EXISTS Stages;

CREATE TABLE IF NOT EXISTS Stages (
    StageID    INTEGER PRIMARY KEY AUTOINCREMENT
                       NOT NULL,
    StageName  TEXT    NOT NULL,
    IsSource   BIGINT  DEFAULT (0),
    IsReserved BIGINT  DEFAULT (0) 
);

INSERT INTO Stages (StageID, StageName, IsSource, IsReserved) VALUES (1, 'DEPLOYED', 0, 1);
INSERT INTO Stages (StageID, StageName, IsSource, IsReserved) VALUES (2, 'NEXUS', 0, 1);

-- Index: ArchiveFormats_ArchiveFormats_sqlite_autoindex_ArchiveFormats_1
DROP INDEX IF EXISTS ArchiveFormats_ArchiveFormats_sqlite_autoindex_ArchiveFormats_1;

CREATE UNIQUE INDEX IF NOT EXISTS ArchiveFormats_ArchiveFormats_sqlite_autoindex_ArchiveFormats_1 ON ArchiveFormats (
    FormatName ASC
);


-- Index: FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_ModID
DROP INDEX IF EXISTS FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_ModID;

CREATE INDEX IF NOT EXISTS FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_ModID ON FileInfo (
    ModID ASC
);


-- Index: FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_PluginID
DROP INDEX IF EXISTS FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_PluginID;

CREATE INDEX IF NOT EXISTS FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_PluginID ON FileInfo (
    PluginID ASC
);


-- Index: FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_StageID
DROP INDEX IF EXISTS FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_StageID;

CREATE INDEX IF NOT EXISTS FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_StageID ON FileInfo (
    StageID ASC
);


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


-- Index: Plugins_Plugins_idx_Plugins_GroupID
DROP INDEX IF EXISTS Plugins_Plugins_idx_Plugins_GroupID;

CREATE INDEX IF NOT EXISTS Plugins_Plugins_idx_Plugins_GroupID ON Plugins (
    GroupID ASC
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


-- Trigger: fki_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID
DROP TRIGGER IF EXISTS fki_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID;
CREATE TRIGGER IF NOT EXISTS fki_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID
                      BEFORE INSERT
                          ON Config
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table Config violates foreign key constraint FK_Config_0_0") 
     WHERE NEW.ArchiveFormatID IS NOT NULL AND 
           (
               SELECT ArchiveFormatID
                 FROM ArchiveFormats
                WHERE ArchiveFormatID = NEW.ArchiveFormatID
           )
           IS NULL;
END;


-- Trigger: fki_ExternalIDs_ModID_ModItems_ModID
DROP TRIGGER IF EXISTS fki_ExternalIDs_ModID_ModItems_ModID;
CREATE TRIGGER IF NOT EXISTS fki_ExternalIDs_ModID_ModItems_ModID
                      BEFORE INSERT
                          ON ExternalIDs
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table ExternalIDs violates foreign key constraint FK_ExternalIDs_1_0") 
     WHERE NEW.ModID IS NOT NULL AND 
           (
               SELECT ModID
                 FROM ModItems
                WHERE ModID = NEW.ModID
           )
           IS NULL;
END;


-- Trigger: fki_ExternalIDs_PluginID_Plugins_PluginID
DROP TRIGGER IF EXISTS fki_ExternalIDs_PluginID_Plugins_PluginID;
CREATE TRIGGER IF NOT EXISTS fki_ExternalIDs_PluginID_Plugins_PluginID
                      BEFORE INSERT
                          ON ExternalIDs
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table ExternalIDs violates foreign key constraint FK_ExternalIDs_0_0") 
     WHERE NEW.PluginID IS NOT NULL AND 
           (
               SELECT PluginID
                 FROM Plugins
                WHERE PluginID = NEW.PluginID
           )
           IS NULL;
END;


-- Trigger: fki_FileInfo_ModID_ModItems_ModID
DROP TRIGGER IF EXISTS fki_FileInfo_ModID_ModItems_ModID;
CREATE TRIGGER IF NOT EXISTS fki_FileInfo_ModID_ModItems_ModID
                      BEFORE INSERT
                          ON FileInfo
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table FileInfo violates foreign key constraint FK_FileInfo_2_0") 
     WHERE NEW.ModID IS NOT NULL AND 
           (
               SELECT ModID
                 FROM ModItems
                WHERE ModID = NEW.ModID
           )
           IS NULL;
END;


-- Trigger: fki_FileInfo_PluginID_Plugins_PluginID
DROP TRIGGER IF EXISTS fki_FileInfo_PluginID_Plugins_PluginID;
CREATE TRIGGER IF NOT EXISTS fki_FileInfo_PluginID_Plugins_PluginID
                      BEFORE INSERT
                          ON FileInfo
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table FileInfo violates foreign key constraint FK_FileInfo_0_0") 
     WHERE NEW.PluginID IS NOT NULL AND 
           (
               SELECT PluginID
                 FROM Plugins
                WHERE PluginID = NEW.PluginID
           )
           IS NULL;
END;


-- Trigger: fki_FileInfo_StageID_Stages_StageID
DROP TRIGGER IF EXISTS fki_FileInfo_StageID_Stages_StageID;
CREATE TRIGGER IF NOT EXISTS fki_FileInfo_StageID_Stages_StageID
                      BEFORE INSERT
                          ON FileInfo
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table FileInfo violates foreign key constraint FK_FileInfo_1_0") 
     WHERE NEW.StageID IS NOT NULL AND 
           (
               SELECT StageID
                 FROM Stages
                WHERE StageID = NEW.StageID
           )
           IS NULL;
END;


-- Trigger: fki_ModGroups_ParentID_ModGroups_GroupID
DROP TRIGGER IF EXISTS fki_ModGroups_ParentID_ModGroups_GroupID;
CREATE TRIGGER IF NOT EXISTS fki_ModGroups_ParentID_ModGroups_GroupID
                      BEFORE INSERT
                          ON ModGroups
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table ModGroups violates foreign key constraint FK_ModGroups_0_0") 
     WHERE NEW.ParentID IS NOT NULL AND 
           (
               SELECT GroupID
                 FROM ModGroups
                WHERE GroupID = NEW.ParentID
           )
           IS NULL;
END;


-- Trigger: fki_ModItems_CurrentStageID_Stages_StageID
DROP TRIGGER IF EXISTS fki_ModItems_CurrentStageID_Stages_StageID;
CREATE TRIGGER IF NOT EXISTS fki_ModItems_CurrentStageID_Stages_StageID
                      BEFORE INSERT
                          ON ModItems
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table ModItems violates foreign key constraint FK_ModItems_0_0") 
     WHERE NEW.CurrentStageID IS NOT NULL AND 
           (
               SELECT StageID
                 FROM Stages
                WHERE StageID = NEW.CurrentStageID
           )
           IS NULL;
END;


-- Trigger: fki_Plugins_GroupID_ModGroups_GroupID
DROP TRIGGER IF EXISTS fki_Plugins_GroupID_ModGroups_GroupID;
CREATE TRIGGER IF NOT EXISTS fki_Plugins_GroupID_ModGroups_GroupID
                      BEFORE INSERT
                          ON Plugins
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table Plugins violates foreign key constraint FK_Plugins_0_0") 
     WHERE NEW.GroupID IS NOT NULL AND 
           (
               SELECT GroupID
                 FROM ModGroups
                WHERE GroupID = NEW.GroupID
           )
           IS NULL;
END;


-- Trigger: fki_ProfilePlugins_PluginID_Plugins_PluginID
DROP TRIGGER IF EXISTS fki_ProfilePlugins_PluginID_Plugins_PluginID;
CREATE TRIGGER IF NOT EXISTS fki_ProfilePlugins_PluginID_Plugins_PluginID
                      BEFORE INSERT
                          ON ProfilePlugins
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_0_0") 
     WHERE (
               SELECT PluginID
                 FROM Plugins
                WHERE PluginID = NEW.PluginID
           )
           IS NULL;
END;


-- Trigger: fki_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID
DROP TRIGGER IF EXISTS fki_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID;
CREATE TRIGGER IF NOT EXISTS fki_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID
                      BEFORE INSERT
                          ON ProfilePlugins
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Insert on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_1_0") 
     WHERE (
               SELECT ProfileID
                 FROM LoadOutProfile
                WHERE ProfileID = NEW.ProfileID
           )
           IS NULL;
END;


-- Trigger: fku_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID
DROP TRIGGER IF EXISTS fku_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID;
CREATE TRIGGER IF NOT EXISTS fku_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID
                      BEFORE UPDATE
                          ON Config
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table Config violates foreign key constraint FK_Config_0_0") 
     WHERE NEW.ArchiveFormatID IS NOT NULL AND 
           (
               SELECT ArchiveFormatID
                 FROM ArchiveFormats
                WHERE ArchiveFormatID = NEW.ArchiveFormatID
           )
           IS NULL;
END;


-- Trigger: fku_ExternalIDs_ModID_ModItems_ModID
DROP TRIGGER IF EXISTS fku_ExternalIDs_ModID_ModItems_ModID;
CREATE TRIGGER IF NOT EXISTS fku_ExternalIDs_ModID_ModItems_ModID
                      BEFORE UPDATE
                          ON ExternalIDs
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table ExternalIDs violates foreign key constraint FK_ExternalIDs_1_0") 
     WHERE NEW.ModID IS NOT NULL AND 
           (
               SELECT ModID
                 FROM ModItems
                WHERE ModID = NEW.ModID
           )
           IS NULL;
END;


-- Trigger: fku_ExternalIDs_PluginID_Plugins_PluginID
DROP TRIGGER IF EXISTS fku_ExternalIDs_PluginID_Plugins_PluginID;
CREATE TRIGGER IF NOT EXISTS fku_ExternalIDs_PluginID_Plugins_PluginID
                      BEFORE UPDATE
                          ON ExternalIDs
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table ExternalIDs violates foreign key constraint FK_ExternalIDs_0_0") 
     WHERE NEW.PluginID IS NOT NULL AND 
           (
               SELECT PluginID
                 FROM Plugins
                WHERE PluginID = NEW.PluginID
           )
           IS NULL;
END;


-- Trigger: fku_FileInfo_ModID_ModItems_ModID
DROP TRIGGER IF EXISTS fku_FileInfo_ModID_ModItems_ModID;
CREATE TRIGGER IF NOT EXISTS fku_FileInfo_ModID_ModItems_ModID
                      BEFORE UPDATE
                          ON FileInfo
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table FileInfo violates foreign key constraint FK_FileInfo_2_0") 
     WHERE NEW.ModID IS NOT NULL AND 
           (
               SELECT ModID
                 FROM ModItems
                WHERE ModID = NEW.ModID
           )
           IS NULL;
END;


-- Trigger: fku_FileInfo_PluginID_Plugins_PluginID
DROP TRIGGER IF EXISTS fku_FileInfo_PluginID_Plugins_PluginID;
CREATE TRIGGER IF NOT EXISTS fku_FileInfo_PluginID_Plugins_PluginID
                      BEFORE UPDATE
                          ON FileInfo
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table FileInfo violates foreign key constraint FK_FileInfo_0_0") 
     WHERE NEW.PluginID IS NOT NULL AND 
           (
               SELECT PluginID
                 FROM Plugins
                WHERE PluginID = NEW.PluginID
           )
           IS NULL;
END;


-- Trigger: fku_FileInfo_StageID_Stages_StageID
DROP TRIGGER IF EXISTS fku_FileInfo_StageID_Stages_StageID;
CREATE TRIGGER IF NOT EXISTS fku_FileInfo_StageID_Stages_StageID
                      BEFORE UPDATE
                          ON FileInfo
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table FileInfo violates foreign key constraint FK_FileInfo_1_0") 
     WHERE NEW.StageID IS NOT NULL AND 
           (
               SELECT StageID
                 FROM Stages
                WHERE StageID = NEW.StageID
           )
           IS NULL;
END;


-- Trigger: fku_ModGroups_ParentID_ModGroups_GroupID
DROP TRIGGER IF EXISTS fku_ModGroups_ParentID_ModGroups_GroupID;
CREATE TRIGGER IF NOT EXISTS fku_ModGroups_ParentID_ModGroups_GroupID
                      BEFORE UPDATE
                          ON ModGroups
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table ModGroups violates foreign key constraint FK_ModGroups_0_0") 
     WHERE NEW.ParentID IS NOT NULL AND 
           (
               SELECT GroupID
                 FROM ModGroups
                WHERE GroupID = NEW.ParentID
           )
           IS NULL;
END;


-- Trigger: fku_ModItems_CurrentStageID_Stages_StageID
DROP TRIGGER IF EXISTS fku_ModItems_CurrentStageID_Stages_StageID;
CREATE TRIGGER IF NOT EXISTS fku_ModItems_CurrentStageID_Stages_StageID
                      BEFORE UPDATE
                          ON ModItems
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table ModItems violates foreign key constraint FK_ModItems_0_0") 
     WHERE NEW.CurrentStageID IS NOT NULL AND 
           (
               SELECT StageID
                 FROM Stages
                WHERE StageID = NEW.CurrentStageID
           )
           IS NULL;
END;


-- Trigger: fku_Plugins_GroupID_ModGroups_GroupID
DROP TRIGGER IF EXISTS fku_Plugins_GroupID_ModGroups_GroupID;
CREATE TRIGGER IF NOT EXISTS fku_Plugins_GroupID_ModGroups_GroupID
                      BEFORE UPDATE
                          ON Plugins
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table Plugins violates foreign key constraint FK_Plugins_0_0") 
     WHERE NEW.GroupID IS NOT NULL AND 
           (
               SELECT GroupID
                 FROM ModGroups
                WHERE GroupID = NEW.GroupID
           )
           IS NULL;
END;


-- Trigger: fku_ProfilePlugins_PluginID_Plugins_PluginID
DROP TRIGGER IF EXISTS fku_ProfilePlugins_PluginID_Plugins_PluginID;
CREATE TRIGGER IF NOT EXISTS fku_ProfilePlugins_PluginID_Plugins_PluginID
                      BEFORE UPDATE
                          ON ProfilePlugins
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_0_0") 
     WHERE (
               SELECT PluginID
                 FROM Plugins
                WHERE PluginID = NEW.PluginID
           )
           IS NULL;
END;


-- Trigger: fku_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID
DROP TRIGGER IF EXISTS fku_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID;
CREATE TRIGGER IF NOT EXISTS fku_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID
                      BEFORE UPDATE
                          ON ProfilePlugins
                    FOR EACH ROW
BEGIN
    SELECT RAISE(ROLLBACK, "Update on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_1_0") 
     WHERE (
               SELECT ProfileID
                 FROM LoadOutProfile
                WHERE ProfileID = NEW.ProfileID
           )
           IS NULL;
END;


-- View: vwConfig
DROP VIEW IF EXISTS vwConfig;
CREATE VIEW IF NOT EXISTS vwConfig AS
    SELECT c.RepoFolder,
           c.UseGit,
           c.GitHubRepo,
           c.GameFolder,
           c.UseModManager,
           c.ModStagingFolder,
           c.ModManagerExecutable,
           c.ModManagerParameters,
           c.IDEExecutable,
           c.LimitFileTypes,
           c.PromoteIncludeFiletypes,
           c.PackageExcludeFiletypes,
           c.TimestampFormat,
           c.MyNamespace,
           c.MyResourcePrefix,
           c.NexusApiKey,
           c.ShowSaveMessage,
           c.ShowOverwriteMessage,
           c.AutoCheckForUpdates,
           (
               SELECT GROUP_CONCAT(CASE WHEN OrderedStages.IsSource = 1 THEN '*' || OrderedStages.StageName WHEN OrderedStages.IsReserved = 1 THEN '#' || OrderedStages.StageName ELSE OrderedStages.StageName END, ', ') 
                 FROM (
                          SELECT s.StageName,
                                 s.IsSource,
                                 s.IsReserved
                            FROM Stages s
                           ORDER BY CASE WHEN s.IsSource = 1 THEN 0 WHEN s.IsReserved = 1 THEN 2 ELSE 1 END,
                                    s.StageName
                      )
                      AS OrderedStages
           )
           AS ModStages,
           af.FormatName AS ArchiveFormat
      FROM Config c
           JOIN
           ArchiveFormats af ON af.ArchiveFormatID = c.ArchiveFormatID;


-- View: vwLoadOuts
DROP VIEW IF EXISTS vwLoadOuts;
CREATE VIEW IF NOT EXISTS vwLoadOuts AS
    SELECT l.ProfileID,
           l.ProfileName,
           p.PluginID,
           p.PluginName,
           p.Description,
           p.Achievements,
           p.DTStamp AS TimeStamp,
           p.Version,
           e.BethesdaID,
           e.NexusID,
           p.GroupID,
           p.GroupOrdinal
      FROM LoadOutProfile l
           JOIN
           ProfilePlugins pp ON l.ProfileID = pp.ProfileID
           JOIN
           Plugins p ON pp.PluginID = p.PluginID
           LEFT JOIN
           ExternalIDs e ON p.PluginID = e.PluginID
     ORDER BY l.ProfileID,
              p.GroupID,
              p.GroupOrdinal;


-- View: vwModAllFiles
DROP VIEW IF EXISTS vwModAllFiles;
CREATE VIEW IF NOT EXISTS vwModAllFiles AS
    SELECT mi.ModID,
           mi.ModName,
           s.StageName,
           fi.Filename,
           fi.RelativePath,
           fi.DTStamp,
           fi.HASH
      FROM ModItems mi
           JOIN
           FileInfo fi ON mi.ModID = fi.ModID
           JOIN
           Stages s ON fi.StageID = s.StageID;


-- View: vwModArchives
DROP VIEW IF EXISTS vwModArchives;
CREATE VIEW IF NOT EXISTS vwModArchives AS
    SELECT mi.ModID,
           mi.ModName,
           s.StageName,
           fi.Filename,
           fi.RelativePath,
           fi.DTStamp,
           fi.HASH
      FROM ModItems mi
           JOIN
           FileInfo fi ON mi.ModID = fi.ModID
           JOIN
           Stages s ON fi.StageID = s.StageID
     WHERE fi.IsArchive = 1;


-- View: vwModFiles
DROP VIEW IF EXISTS vwModFiles;
CREATE VIEW IF NOT EXISTS vwModFiles AS
    SELECT mi.ModID,
           mi.ModName,
           s.StageName,
           fi.Filename,
           fi.RelativePath,
           fi.DTStamp,
           fi.HASH
      FROM ModItems mi
           JOIN
           FileInfo fi ON mi.ModID = fi.ModID
           JOIN
           Stages s ON fi.StageID = s.StageID
     WHERE fi.IsArchive = 0;


-- View: vwModGroups
DROP VIEW IF EXISTS vwModGroups;
CREATE VIEW IF NOT EXISTS vwModGroups AS
    SELECT g.GroupID,
           g.Ordinal,
           g.Description AS GroupDescription,
           g.ParentID,
           p.PluginID,
           p.PluginName,
           p.Description AS PluginDescription,
           p.Achievements,
           p.DTStamp AS TimeStamp,
           p.Version,
           e.BethesdaID,
           e.NexusID,
           p.GroupOrdinal
      FROM ModGroups g
           JOIN
           Plugins p ON g.GroupID = p.GroupID
           LEFT JOIN
           ExternalIDs e ON p.PluginID = e.PluginID
     ORDER BY g.ParentID,
              g.Ordinal,
              p.GroupOrdinal;


-- View: vwModItems
DROP VIEW IF EXISTS vwModItems;
CREATE VIEW IF NOT EXISTS vwModItems AS
    SELECT mi.ModID,
           mi.ModName,
           mi.ModFolderPath,
           s.StageName AS CurrentStage,
           e.NexusID,
           e.BethesdaID
      FROM ModItems mi
           LEFT JOIN
           Stages s ON mi.CurrentStageID = s.StageID
           LEFT JOIN
           ExternalIDs e ON mi.ModID = e.ModID
     ORDER BY ModName;


-- View: vwModNewestArchives
DROP VIEW IF EXISTS vwModNewestArchives;
CREATE VIEW IF NOT EXISTS vwModNewestArchives AS
    SELECT ma.ModID,
           ma.ModName,
           ma.StageName,
           ma.Filename,
           ma.RelativePath,
           MAX(ma.DTStamp) AS DTStamp,
           ma.HASH
      FROM vwModArchives ma
     GROUP BY ma.ModID,
              ma.ModName,
              ma.StageName,
              ma.Filename,
              ma.RelativePath,
              ma.HASH;


-- View: vwModStages
DROP VIEW IF EXISTS vwModStages;
CREATE VIEW IF NOT EXISTS vwModStages AS
    SELECT ModItems.ModName,
           Stages.StageName
      FROM ModStages
           JOIN
           ModItems ON ModStages.ModID = ModItems.ModID
           JOIN
           Stages ON ModStages.StageID = Stages.StageID;


COMMIT TRANSACTION;
PRAGMA foreign_keys = on;
