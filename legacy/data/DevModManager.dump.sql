PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE [ProfilePlugins] ( 
  [ProfileID] bigint NOT NULL 
, [PluginID] bigint NOT NULL 
, CONSTRAINT [sqlite_autoindex_ProfilePlugins_1] PRIMARY KEY ([ProfileID],[PluginID]) 
, CONSTRAINT [FK_ProfilePlugins_0_0] FOREIGN KEY ([PluginID]) REFERENCES [Plugins] ([PluginID]) ON DELETE CASCADE ON UPDATE NO ACTION 
, CONSTRAINT [FK_ProfilePlugins_1_0] FOREIGN KEY ([ProfileID]) REFERENCES [LoadOutProfile] ([ProfileID]) ON DELETE CASCADE ON UPDATE NO ACTION 
);
CREATE TABLE [ArchiveFormats] ( 
  [ArchiveFormatID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [FormatName] text NOT NULL UNIQUE 
);
INSERT INTO ArchiveFormats VALUES(1,'zip');
INSERT INTO ArchiveFormats VALUES(2,'7z');
CREATE TABLE [ExternalIDs] ( 
  [ExternalID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [PluginID] bigint NULL 
, [ModID] bigint NULL 
, [BethesdaID] text NULL UNIQUE 
, [NexusID] text NULL UNIQUE 
, CONSTRAINT [FK_ExternalIDs_0_0] FOREIGN KEY ([ModID]) REFERENCES [ModItems] ([ModID]) ON DELETE NO ACTION ON UPDATE NO ACTION 
, CONSTRAINT [FK_ExternalIDs_1_0] FOREIGN KEY ([PluginID]) REFERENCES [Plugins] ([PluginID]) ON DELETE NO ACTION ON UPDATE NO ACTION 
);
CREATE TABLE [LoadOutProfile] ( 
  [ProfileID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [ProfileName] text NOT NULL UNIQUE 
);
CREATE TABLE [ModGroups] ( 
  [GroupID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [Ordinal] INTEGER NULL 
, [GroupName] text NULL UNIQUE 
, [Description] text NULL 
, [ParentID] INTEGER NULL 
, CONSTRAINT [FK_ModGroups_0_0] FOREIGN KEY ([ParentID]) REFERENCES [ModGroups] ([GroupID]) ON DELETE SET NULL ON UPDATE NO ACTION 
);
CREATE TABLE [Plugins] ( 
  [PluginID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [PluginName] text NULL UNIQUE 
, [Description] text NULL 
, [Achievements] text NULL 
, [DTStamp] text NOT NULL 
, [Version] text NULL 
, [GroupID] bigint NULL 
, [GroupOrdinal] bigint NULL 
, CONSTRAINT [FK_Plugins_0_0] FOREIGN KEY ([GroupID]) REFERENCES [ModGroups] ([GroupID]) ON DELETE SET NULL ON UPDATE NO ACTION 
);
CREATE TABLE [FileInfo] ( 
  [FileID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [PluginID] bigint NULL 
, [ModID] bigint NULL 
, [StageID] bigint NULL 
, [Filename] text NOT NULL 
, [RelativePath] text NULL 
, [DTStamp] datetime NOT NULL 
, [HASH] text NULL 
, [IsArchive] bigint NOT NULL 
, CONSTRAINT [FK_FileInfo_0_0] FOREIGN KEY ([ModID]) REFERENCES [ModItems] ([ModID]) ON DELETE CASCADE ON UPDATE NO ACTION 
, CONSTRAINT [FK_FileInfo_1_0] FOREIGN KEY ([StageID]) REFERENCES [Stages] ([StageID]) ON DELETE NO ACTION ON UPDATE NO ACTION 
, CONSTRAINT [FK_FileInfo_2_0] FOREIGN KEY ([PluginID]) REFERENCES [Plugins] ([PluginID]) ON DELETE NO ACTION ON UPDATE NO ACTION 
);
CREATE TABLE [InitializationStatus] ( 
  [Id] bigint NOT NULL 
, [IsInitialized] bigint NOT NULL 
, [InitializationTime] text NOT NULL 
, CONSTRAINT [sqlite_autoindex_InitializationStatus_1] PRIMARY KEY ([Id]) 
);
CREATE TABLE [ModItems] ( 
  [ModID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [ModName] text NOT NULL 
, [ModFolderPath] text NOT NULL 
, [CurrentStageID] bigint NULL 
, CONSTRAINT [FK_ModItems_0_0] FOREIGN KEY ([CurrentStageID]) REFERENCES [Stages] ([StageID]) ON DELETE SET NULL ON UPDATE NO ACTION 
);
CREATE TABLE [ModStages] ( 
  [ModID] bigint NOT NULL 
, [StageID] bigint NOT NULL 
, CONSTRAINT [sqlite_autoindex_ModStages_1] PRIMARY KEY ([ModID],[StageID]) 
, CONSTRAINT [FK_ModStages_0_0] FOREIGN KEY ([StageID]) REFERENCES [Stages] ([StageID]) ON DELETE NO ACTION ON UPDATE NO ACTION 
, CONSTRAINT [FK_ModStages_1_0] FOREIGN KEY ([ModID]) REFERENCES [ModItem] ([ModID]) ON DELETE NO ACTION ON UPDATE NO ACTION 
);
CREATE TABLE [Stages] ( 
  [StageID] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL 
, [StageName] text NOT NULL 
, [IsSource] bigint DEFAULT (0) NULL 
, [IsReserved] bigint DEFAULT (0) NULL 
);
INSERT INTO Stages VALUES(1,'DEPLOYED',0,1);
INSERT INTO Stages VALUES(2,'NEXUS',0,1);
CREATE TABLE Config (RepoFolder text NOT NULL, UseGit bigint DEFAULT (1), GitHubRepo text, UseModManager bigint DEFAULT (1), GameFolder text, ModStagingFolder text, ModManagerExecutable text, ModManagerParameters text, IDEExecutable text, LimitFileTypes bigint DEFAULT (1), PromoteIncludeFiletypes text, PackageExcludeFiletypes text, ArchiveFormatID bigint, TimestampFormat text, MyNameSpace text, MyResourcePrefix text, ShowSaveMessage bigint DEFAULT (0), ShowOverwriteMessage bigint DEFAULT (0), NexusAPIKey text, AutoCheckForUpdates bigint DEFAULT (1), GitHubUsername TEXT, GitHubToken TEXT, GitHubTokenExpiration TEXT, DarkMode INTEGER DEFAULT (1), CONSTRAINT FK_Config_0_0 FOREIGN KEY (ArchiveFormatID) REFERENCES ArchiveFormats (ArchiveFormatID) ON DELETE SET NULL ON UPDATE NO ACTION);
PRAGMA writable_schema=ON;
CREATE TABLE IF NOT EXISTS sqlite_sequence(name,seq);
DELETE FROM sqlite_sequence;
INSERT INTO sqlite_sequence VALUES('ArchiveFormats',2);
INSERT INTO sqlite_sequence VALUES('Stages',2);
CREATE VIEW vwLoadOuts AS     
SELECT     
    l.ProfileID,     
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
FROM     
    LoadOutProfile l     
JOIN     
    ProfilePlugins pp ON l.ProfileID = pp.ProfileID     
JOIN     
    Plugins p ON pp.PluginID = p.PluginID     
LEFT JOIN     
    ExternalIDs e ON p.PluginID = e.PluginID   
ORDER BY l.ProfileID, p.GroupID, p.GroupOrdinal;
CREATE VIEW vwModAllFiles AS     
SELECT        
    mi.ModID,        
    mi.ModName,    
    s.StageName,        
    fi.Filename,        
    fi.RelativePath,        
    fi.DTStamp,        
    fi.HASH        
FROM        
    ModItems mi        
JOIN        
    FileInfo fi ON mi.ModID = fi.ModID     
JOIN    
    Stages s ON fi.StageID = s.StageID;
CREATE VIEW vwModArchives AS    
SELECT       
    mi.ModID,       
    mi.ModName,   
    s.StageName,       
    fi.Filename,       
    fi.RelativePath,       
    fi.DTStamp,       
    fi.HASH       
FROM       
    ModItems mi       
JOIN       
    FileInfo fi ON mi.ModID = fi.ModID    
JOIN   
    Stages s ON fi.StageID = s.StageID   
WHERE    
    fi.IsArchive = 1;
CREATE VIEW vwModFiles AS    
SELECT       
    mi.ModID,       
    mi.ModName,   
    s.StageName,       
    fi.Filename,       
    fi.RelativePath,       
    fi.DTStamp,       
    fi.HASH       
FROM       
    ModItems mi       
JOIN       
    FileInfo fi ON mi.ModID = fi.ModID    
JOIN   
    Stages s ON fi.StageID = s.StageID   
WHERE    
    fi.IsArchive = 0;
CREATE VIEW vwModNewestArchives AS     
SELECT       
    ma.ModID,       
    ma.ModName,   
    ma.StageName,       
    ma.Filename,       
    ma.RelativePath,       
    MAX(ma.DTStamp) AS DTStamp,       
    ma.HASH     
FROM       
    vwModArchives ma     
GROUP BY       
    ma.ModID,       
    ma.ModName,       
    ma.StageName,       
    ma.Filename,       
    ma.RelativePath,       
    ma.HASH;
CREATE VIEW vwModGroups AS    
SELECT     
     g.GroupID,     
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
 FROM     
     ModGroups g     
 JOIN     
     Plugins p ON g.GroupID = p.GroupID     
 LEFT JOIN     
     ExternalIDs e ON p.PluginID = e.PluginID    
 ORDER BY g.ParentID, g.Ordinal, p.GroupOrdinal;
CREATE VIEW vwModItems AS         
SELECT         
    mi.ModID,         
    mi.ModName,    
	mi.ModFolderPath,        
    s.StageName AS CurrentStage,         
    e.NexusID,         
    e.BethesdaID         
FROM         
    ModItems mi         
LEFT JOIN         
    Stages s ON mi.CurrentStageID = s.StageID         
LEFT JOIN         
    ExternalIDs e ON mi.ModID = e.ModID 
ORDER BY ModName;
CREATE VIEW vwModStages AS 
SELECT  
    ModItems.ModName, 
    Stages.StageName 
FROM  
    ModStages 
JOIN  
    ModItems ON ModStages.ModID = ModItems.ModID 
JOIN  
    Stages ON ModStages.StageID = Stages.StageID;
CREATE VIEW vwConfig AS SELECT          
    c.RepoFolder,        
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
 c.DarkMode,
 c.GitHubUsername,
 c.GitHubToken,
 c.GitHubTokenExpiration,     
    (    
        SELECT GROUP_CONCAT(    
            CASE     
                WHEN OrderedStages.IsSource = 1 THEN '*' || OrderedStages.StageName         
                WHEN OrderedStages.IsReserved = 1 THEN '#' || OrderedStages.StageName         
                ELSE OrderedStages.StageName         
            END, ', '         
        )         
        FROM (         
            SELECT s.StageName, s.IsSource, s.IsReserved         
            FROM Stages s         
            ORDER BY     
                CASE     
                    WHEN s.IsSource = 1 THEN 0         
                    WHEN s.IsReserved = 1 THEN 2         
                    ELSE 1         
                END, s.StageName         
        ) AS OrderedStages         
    ) AS ModStages,         
    af.FormatName AS ArchiveFormat         
FROM Config c         
JOIN ArchiveFormats af ON af.ArchiveFormatID = c.ArchiveFormatID;
CREATE TRIGGER [fki_ProfilePlugins_PluginID_Plugins_PluginID] BEFORE Insert ON [ProfilePlugins] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_0_0') WHERE (SELECT PluginID FROM Plugins WHERE  PluginID = NEW.PluginID) IS NULL; END;
CREATE TRIGGER [fku_ProfilePlugins_PluginID_Plugins_PluginID] BEFORE Update ON [ProfilePlugins] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_0_0') WHERE (SELECT PluginID FROM Plugins WHERE  PluginID = NEW.PluginID) IS NULL; END;
CREATE TRIGGER [fki_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID] BEFORE Insert ON [ProfilePlugins] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_1_0') WHERE (SELECT ProfileID FROM LoadOutProfile WHERE  ProfileID = NEW.ProfileID) IS NULL; END;
CREATE TRIGGER [fku_ProfilePlugins_ProfileID_LoadOutProfile_ProfileID] BEFORE Update ON [ProfilePlugins] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table ProfilePlugins violates foreign key constraint FK_ProfilePlugins_1_0') WHERE (SELECT ProfileID FROM LoadOutProfile WHERE  ProfileID = NEW.ProfileID) IS NULL; END;
CREATE TRIGGER [fki_ExternalIDs_PluginID_Plugins_PluginID] BEFORE Insert ON [ExternalIDs] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table ExternalIDs violates foreign key constraint FK_ExternalIDs_0_0') WHERE NEW.PluginID IS NOT NULL AND(SELECT PluginID FROM Plugins WHERE  PluginID = NEW.PluginID) IS NULL; END;
CREATE TRIGGER [fku_ExternalIDs_PluginID_Plugins_PluginID] BEFORE Update ON [ExternalIDs] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table ExternalIDs violates foreign key constraint FK_ExternalIDs_0_0') WHERE NEW.PluginID IS NOT NULL AND(SELECT PluginID FROM Plugins WHERE  PluginID = NEW.PluginID) IS NULL; END;
CREATE TRIGGER [fki_ExternalIDs_ModID_ModItems_ModID] BEFORE Insert ON [ExternalIDs] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table ExternalIDs violates foreign key constraint FK_ExternalIDs_1_0') WHERE NEW.ModID IS NOT NULL AND(SELECT ModID FROM ModItems WHERE  ModID = NEW.ModID) IS NULL; END;
CREATE TRIGGER [fku_ExternalIDs_ModID_ModItems_ModID] BEFORE Update ON [ExternalIDs] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table ExternalIDs violates foreign key constraint FK_ExternalIDs_1_0') WHERE NEW.ModID IS NOT NULL AND(SELECT ModID FROM ModItems WHERE  ModID = NEW.ModID) IS NULL; END;
CREATE TRIGGER [fki_ModGroups_ParentID_ModGroups_GroupID] BEFORE Insert ON [ModGroups] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table ModGroups violates foreign key constraint FK_ModGroups_0_0') WHERE NEW.ParentID IS NOT NULL AND(SELECT GroupID FROM ModGroups WHERE  GroupID = NEW.ParentID) IS NULL; END;
CREATE TRIGGER [fku_ModGroups_ParentID_ModGroups_GroupID] BEFORE Update ON [ModGroups] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table ModGroups violates foreign key constraint FK_ModGroups_0_0') WHERE NEW.ParentID IS NOT NULL AND(SELECT GroupID FROM ModGroups WHERE  GroupID = NEW.ParentID) IS NULL; END;
CREATE TRIGGER [fki_Plugins_GroupID_ModGroups_GroupID] BEFORE Insert ON [Plugins] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table Plugins violates foreign key constraint FK_Plugins_0_0') WHERE NEW.GroupID IS NOT NULL AND(SELECT GroupID FROM ModGroups WHERE  GroupID = NEW.GroupID) IS NULL; END;
CREATE TRIGGER [fku_Plugins_GroupID_ModGroups_GroupID] BEFORE Update ON [Plugins] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table Plugins violates foreign key constraint FK_Plugins_0_0') WHERE NEW.GroupID IS NOT NULL AND(SELECT GroupID FROM ModGroups WHERE  GroupID = NEW.GroupID) IS NULL; END;
CREATE TRIGGER [fki_FileInfo_PluginID_Plugins_PluginID] BEFORE Insert ON [FileInfo] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table FileInfo violates foreign key constraint FK_FileInfo_0_0') WHERE NEW.PluginID IS NOT NULL AND(SELECT PluginID FROM Plugins WHERE  PluginID = NEW.PluginID) IS NULL; END;
CREATE TRIGGER [fku_FileInfo_PluginID_Plugins_PluginID] BEFORE Update ON [FileInfo] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table FileInfo violates foreign key constraint FK_FileInfo_0_0') WHERE NEW.PluginID IS NOT NULL AND(SELECT PluginID FROM Plugins WHERE  PluginID = NEW.PluginID) IS NULL; END;
CREATE TRIGGER [fki_FileInfo_StageID_Stages_StageID] BEFORE Insert ON [FileInfo] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table FileInfo violates foreign key constraint FK_FileInfo_1_0') WHERE NEW.StageID IS NOT NULL AND(SELECT StageID FROM Stages WHERE  StageID = NEW.StageID) IS NULL; END;
CREATE TRIGGER [fku_FileInfo_StageID_Stages_StageID] BEFORE Update ON [FileInfo] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table FileInfo violates foreign key constraint FK_FileInfo_1_0') WHERE NEW.StageID IS NOT NULL AND(SELECT StageID FROM Stages WHERE  StageID = NEW.StageID) IS NULL; END;
CREATE TRIGGER [fki_FileInfo_ModID_ModItems_ModID] BEFORE Insert ON [FileInfo] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table FileInfo violates foreign key constraint FK_FileInfo_2_0') WHERE NEW.ModID IS NOT NULL AND(SELECT ModID FROM ModItems WHERE  ModID = NEW.ModID) IS NULL; END;
CREATE TRIGGER [fku_FileInfo_ModID_ModItems_ModID] BEFORE Update ON [FileInfo] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table FileInfo violates foreign key constraint FK_FileInfo_2_0') WHERE NEW.ModID IS NOT NULL AND(SELECT ModID FROM ModItems WHERE  ModID = NEW.ModID) IS NULL; END;
CREATE TRIGGER [fki_ModItems_CurrentStageID_Stages_StageID] BEFORE Insert ON [ModItems] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table ModItems violates foreign key constraint FK_ModItems_0_0') WHERE NEW.CurrentStageID IS NOT NULL AND(SELECT StageID FROM Stages WHERE  StageID = NEW.CurrentStageID) IS NULL; END;
CREATE TRIGGER [fku_ModItems_CurrentStageID_Stages_StageID] BEFORE Update ON [ModItems] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table ModItems violates foreign key constraint FK_ModItems_0_0') WHERE NEW.CurrentStageID IS NOT NULL AND(SELECT StageID FROM Stages WHERE  StageID = NEW.CurrentStageID) IS NULL; END;
CREATE TRIGGER fki_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID BEFORE INSERT ON Config FOR EACH ROW BEGIN SELECT RAISE (ROLLBACK, 'Insert on table Config violates foreign key constraint FK_Config_0_0') WHERE NEW.ArchiveFormatID IS NOT NULL AND (SELECT ArchiveFormatID FROM ArchiveFormats WHERE ArchiveFormatID = NEW.ArchiveFormatID) IS NULL; END;
CREATE TRIGGER fku_Config_ArchiveFormatID_ArchiveFormats_ArchiveFormatID BEFORE UPDATE ON Config FOR EACH ROW BEGIN SELECT RAISE (ROLLBACK, 'Update on table Config violates foreign key constraint FK_Config_0_0') WHERE NEW.ArchiveFormatID IS NOT NULL AND (SELECT ArchiveFormatID FROM ArchiveFormats WHERE ArchiveFormatID = NEW.ArchiveFormatID) IS NULL; END;
CREATE UNIQUE INDEX [ArchiveFormats_ArchiveFormats_sqlite_autoindex_ArchiveFormats_1] ON [ArchiveFormats] ([FormatName] ASC);
CREATE INDEX [Plugins_Plugins_idx_Plugins_GroupID] ON [Plugins] ([GroupID] ASC);
CREATE INDEX [FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_ModID] ON [FileInfo] ([ModID] ASC);
CREATE INDEX [FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_StageID] ON [FileInfo] ([StageID] ASC);
CREATE INDEX [FileInfo_FileInfo_FileInfo_FileInfo_idx_FileInfo_PluginID] ON [FileInfo] ([PluginID] ASC);
CREATE UNIQUE INDEX [ModItems_ModItems_sqlite_autoindex_ModItems_1] ON [ModItems] ([ModName] ASC);
CREATE INDEX [ModItems_ModItems_ModItems_ModItems_idx_ModItems_CurrentStageID] ON [ModItems] ([CurrentStageID] ASC);
CREATE UNIQUE INDEX [ModItems_sqlite_autoindex_ModItems_1] ON [ModItems] ([ModName] ASC);
CREATE UNIQUE INDEX [Stages_Stages_sqlite_autoindex_Stages_1] ON [Stages] ([StageName] ASC);
CREATE UNIQUE INDEX [Stages_sqlite_autoindex_Stages_1] ON [Stages] ([StageName] ASC);
PRAGMA writable_schema=OFF;
COMMIT;
