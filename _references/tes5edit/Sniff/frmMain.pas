{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

{$WARN SYMBOL_PLATFORM OFF}
unit frmMain;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  System.IniFiles, Vcl.Graphics,
  Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, Vcl.ExtCtrls,
  Vcl.Imaging.pngimage, Vcl.ComCtrls, Diagnostics, SniffProcessor, Vcl.Mask,
  Vcl.Menus, Vcl.Themes;

const
  sSniffVersion = '1.8';
  sSniffCaption = 'S''Lanter''s NIF Helper';
  sSniffTitle = sSniffCaption + ' ' + sSniffVersion;
  WM_PROCESSING_START = WM_USER + 10;

type
  TFormMain = class(TForm)
    pnlInput: TPanel;
    chkInputSubdir: TCheckBox;
    Image1: TImage;
    pnlOutput: TPanel;
    Image2: TImage;
    pnlControl: TPanel;
    btnExit: TButton;
    btnProcess: TButton;
    pnlOperation: TPanel;
    lvProcs: TListView;
    pnlProcFrame: TPanel;
    Label1: TLabel;
    lblSupportedGames: TLabel;
    lblProcessedFilesTitle: TLabel;
    lblProcessedFiles: TLabel;
    tcMain: TTabControl;
    btnInputBrowse: TButton;
    btnOutputBrowse: TButton;
    chkOutputAll: TCheckBox;
    chkSkipOnErrors: TCheckBox;
    edPathContains: TLabeledEdit;
    edThreads: TLabeledEdit;
    menuPopup: TPopupMenu;
    mniStyle: TMenuItem;
    edInput: TComboBox;
    Label2: TLabel;
    edOutput: TComboBox;
    Label3: TLabel;
    procedure FormCreate(Sender: TObject);
    procedure lvProcsSelectItem(Sender: TObject; Item: TListItem;
      Selected: Boolean);
    procedure tcMainChange(Sender: TObject);
    procedure btnInputBrowseClick(Sender: TObject);
    procedure btnOutputBrowseClick(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure btnProcessClick(Sender: TObject);
    procedure btnExitClick(Sender: TObject);
    procedure lblProcessedFilesTitleClick(Sender: TObject);
    procedure mniStyleClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
    FrameMessages: TFrame;
    Settings: TMemIniFile;
    Manager: TProcManager;
    ProcessedFiles: TArray<String>;
    Procs: array of TProcBase;
    Proc: TProcBase;
    ProcFrame: TFrame;
    bAutoMode: Boolean;
    procedure SniffMessage(const aText: string);
    procedure AddProc(const aGroup: string; aProc: TProcBase);

    procedure CreateWnd; override;
    procedure DestroyWnd; override;
    procedure WMDropFiles(var msg: TWMDropFiles); message WM_DROPFILES;
    procedure WMProcessingStart(var msg: TMessage); message WM_PROCESSING_START;
  end;

var
  FormMain: TFormMain;

implementation

{$R *.dfm}

uses
  System.IOUtils,
  System.StrUtils,
  ShellApi,
  wbTaskProgress,
  frMessages,
  wbCommandLine,
  ProcTangents,
  ProcUpdateBounds,
  ProcReplaceAssets,
  ProcRenameStrings,
  ProcCheckForErrors,
  ProcAnalyzeMesh,
  ProcJsonConverter,
  ProcOptimize,
  ProcAdjustTransform,
  ProcApplyTransform,
  ProcJamAnim,
  ProcWeiExplosion,
  ProcAttachParent,
  ProcCopyControlledBlocks,
  ProcCopyPriorities,
  ProcRenameControlledBlocks,
  ProcRemoveControlledBlocks,
  ProcPriorityControlledBlocks,
  ProcAnimQuadraticToLinear,
  ProcAnimSkeletonDeath,
  ProcShaderFlagsUpdate,
  ProcInertiaUpdate,
  ProcRagdollConstraintUpdate,
  ProcMoppUpdate,
  ProcUnweldedVertices,
  ProcFindSeveralStrips,
  ProcFindDrawCalls,
  ProcFindUVs,
  ProcHavokInfo,
  ProcHavokSettingsUpdate,
  ProcHavokSearchMaterial,
  ProcCopyGeometryBlocks,
  ProcVertexPaint,
  ProcGroupShapes,
  ProcChangePartitionSlot,
  ProcFixExportedKFAnim,
  ProcOptimizeKF,
  ProcRemoveNodes,
  ProcRemoveUnusedNodes,
  ProcConvertRootNode,
  ProcUnskinMesh,
  ProcMergeShapes,
  ProcWallsReflectionFlag,
  ProcSoftParticles,
  ProcUniversalTweaker,
  ProcUniversalFixer,
  ProcAddHeadtrackingAnim,
  ProcAddFacialAnim,
  ProcAddBoundingBox,
  ProcAddLODNode,
  ProcAddRootCollisionNode,
  ProcSetMissingNames;


procedure TFormMain.CreateWnd;
begin
  inherited;
  DragAcceptFiles(WindowHandle, True);
end;

procedure TFormMain.DestroyWnd;
begin
  DragAcceptFiles(WindowHandle, False);
  inherited;
end;

procedure TFormMain.WMDropFiles(var msg: TWMDropFiles);
var
  i, cnt: integer;
  fileName: array[0..MAX_PATH] of char;
  f, ff: string;
  sl: TStringList;
  bFirstFile: Boolean;
begin
  sl := TStringList.Create;
  sl.Duplicates := dupIgnore;
  bFirstFile := True;
  try
    cnt := DragQueryFile(msg.Drop, $FFFFFFFF, fileName, MAX_PATH);
    for i := 0 to Pred(cnt) do begin
      DragQueryFile(msg.Drop, i, fileName, MAX_PATH);
      f := fileName;

      if bFirstFile then begin
        Manager.InputDirectory := ExtractFilePath(f);
        Manager.OutputDirectory := ExtractFilePath(f);
        bFirstFile := False;
      end;

      if TFileAttribute.faDirectory in TPath.GetAttributes(f) then begin
        for ff in TDirectory.GetFiles(f, '*.*', TSearchOption.soAllDirectories) do
          sl.Add(ff);
      end else
        sl.Add(f);
    end;

    SetLength(ProcessedFiles, sl.Count);
    for i := 0 to Pred(sl.Count) do
      ProcessedFiles[i] := sl[i];

    if Length(ProcessedFiles) > 0 then
      PostMessage(Handle, WM_PROCESSING_START, 0, 0);

  finally
    DragFinish(msg.Drop);
    sl.Free;
  end;
end;

procedure TFormMain.WMProcessingStart(var msg: TMessage);
begin
  tcMain.TabIndex := 0;
  tcMainChange(nil);
  btnProcessClick(nil);
end;

procedure TFormMain.SniffMessage(const aText: string);
begin
  with TTaskDialog.Create(Self) do try
    Text := aText;
    Caption := Application.Title;
    Flags := [tfUseHiconMain, tfPositionRelativeToWindow, tfAllowDialogCancellation];
    CustomMainIcon := Application.Icon;
    CommonButtons := [tcbClose];
    Execute;
  finally
    Free;
  end;
end;

procedure TFormMain.AddProc(const aGroup: string; aProc: TProcBase);
begin
  SetLength(Procs, Succ(Length(Procs)));
  Procs[Pred(Length(Procs))] := aProc;
  with lvProcs.Items.Add do begin
    Caption := aProc.Title;
    for var i := 0 to Pred(lvProcs.Groups.Count) do
      if lvProcs.Groups[i].Header = aGroup then
        GroupID := lvProcs.Groups[i].GroupID;
  end;
end;

procedure TFormMain.btnInputBrowseClick(Sender: TObject);
begin
  var path: string := edInput.Text;

  if path = '' then
    path := ExtractFilePath(Application.ExeName);

  if SelectFolder(path) then
    edInput.Text := Path;
end;

procedure TFormMain.btnOutputBrowseClick(Sender: TObject);
begin
  var path: string := edOutput.Text;

  if path = '' then
    path := ExtractFilePath(Application.ExeName);

  if SelectFolder(path) then
    edOutput.Text := Path;
end;

procedure TFormMain.FormCreate(Sender: TObject);
var
  LastUsedProc, s: string;
begin
  FormatSettings.DecimalSeparator := '.';

  // custom settings file from the command line
  if wbFindCmdLineParam('S', s) and (s <> '') then begin
    if not TPath.IsPathRooted(s) then
      s := TPath.Combine(ExtractFilePath(ParamStr(0)), s);
  end else
    s := ChangeFileExt(Application.ExeName, '.ini');

  Settings := TMemIniFile.Create(s);

  var theme := TStyleManager.ActiveStyle.Name;
  theme := Settings.ReadString('Main', 'Theme', theme);
  TStyleManager.TrySetStyle(theme);

  for s in TStyleManager.StyleNames do begin
    var m := TMenuItem.Create(menuPopup);
    m.Caption := s;
    m.OnClick := mniStyleClick;
    m.Checked := TStyleManager.ActiveStyle.Name = s;
    mniStyle.Add(m);
  end;

  Manager := TProcManager.Create;
  Manager.SetIniFile(Settings);
  //Manager.AddMessage(Application.Title);
  //Manager.AddMessage('by Zilav, with love for awesome New Vegas mod makers');

  with lvProcs.Groups.Add do begin Header := 'NIF'; State := [lgsNormal, lgsCollapsible]; end;
  with lvProcs.Groups.Add do begin Header := 'Report'; State := [lgsNormal, lgsCollapsible]; end;
  with lvProcs.Groups.Add do begin Header := 'Animation'; State := [lgsNormal, lgsCollapsible]; end;
  with lvProcs.Groups.Add do begin Header := 'Collision'; State := [lgsNormal, lgsCollapsible]; end;
  with lvProcs.Groups.Add do begin Header := 'Shader'; State := [lgsNormal, lgsCollapsible]; end;

  AddProc('NIF', TProcTangents.Create(Manager));
  AddProc('NIF', TProcUpdateBounds.Create(Manager));
  AddProc('NIF', TProcOptimize.Create(Manager));
  AddProc('NIF', TProcReplaceAssets.Create(Manager));
  AddProc('NIF', TProcJsonConverter.Create(Manager));
  AddProc('NIF', TProcUniversalTweaker.Create(Manager));
  AddProc('NIF', TProcUniversalFixer.Create(Manager));
  AddProc('NIF', TProcApplyTransform.Create(Manager));
  AddProc('NIF', TProcAdjustTransform.Create(Manager));
  AddProc('NIF', TProcRenameStrings.Create(Manager));
  AddProc('NIF', TProcAttachParent.Create(Manager));
  AddProc('NIF', TProcCopyGeometryBlocks.Create(Manager));
  AddProc('NIF', TProcVertexPaint.Create(Manager));
  AddProc('NIF', TProcGroupShapes.Create(Manager));
  AddProc('NIF', TProcMergeShapes.Create(Manager));
  AddProc('NIF', TProcRemoveNodes.Create(Manager));
  AddProc('NIF', TProcRemoveUnusedNodes.Create(Manager));
  AddProc('NIF', TProcConvertRootNode.Create(Manager));
  AddProc('NIF', TProcUnskinMesh.Create(Manager));
  AddProc('NIF', TProcChangePartitionSlot.Create(Manager));
  AddProc('NIF', TProcAddLODNode.Create(Manager));
  AddProc('NIF', TProcAddRootCollisionNode.Create(Manager));
  AddProc('NIF', TProcAddBoundingBox.Create(Manager));
  AddProc('NIF', TProcSetMissingNames.Create(Manager));

  AddProc('Report', TProcCheckForErrors.Create(Manager));
  AddProc('Report', TProcAnalyzeMesh.Create(Manager));
  AddProc('Report', TProcHavokInfo.Create(Manager));
  AddProc('Report', TProcUnweldedVertices.Create(Manager));
  AddProc('Report', TProcFindSeveralStrips.Create(Manager));
  AddProc('Report', TProcFindDrawCalls.Create(Manager));
  AddProc('Report', TProcFindUVs.Create(Manager));

  AddProc('Animation', TProcCopyControlledBlocks.Create(Manager));
  AddProc('Animation', TProcCopyPriorities.Create(Manager));
  AddProc('Animation', TProcRenameControlledBlocks.Create(Manager));
  AddProc('Animation', TProcRemoveControlledBlocks.Create(Manager));
  AddProc('Animation', TProcPriorityControlledBlocks.Create(Manager));
  AddProc('Animation', TProcAnimQuadraticToLinear.Create(Manager));
  AddProc('Animation', TProcFixExportedKFAnim.Create(Manager));
  AddProc('Animation', TProcOptimizeKF.Create(Manager));
  AddProc('Animation', TProcAddHeadtrackingAnim.Create(Manager));
  AddProc('Animation', TProcAddFacialAnim.Create(Manager));
  AddProc('Animation', TProcJamAnim.Create(Manager));
  AddProc('Animation', TProcWeiExplosion.Create(Manager));
  AddProc('Animation', TProcAnimSkeletonDeath.Create(Manager));

  AddProc('Collision', TProcMoppUpdate.Create(Manager));
  AddProc('Collision', TProcHavokSettingsUpdate.Create(Manager));
  AddProc('Collision', TProcInertiaUpdate.Create(Manager));
  AddProc('Collision', TProcRagdollConstraintUpdate.Create(Manager));
  AddProc('Collision', TProcHavokSearchMaterial.Create(Manager));

  AddProc('Shader', TProcShaderFlagsUpdate.Create(Manager));
  AddProc('Shader', TProcWallsReflectionFlag.Create(Manager));
  AddProc('Shader', TProcSoftParticles.Create(Manager));

  //ShowScrollBar(lvProcs.Handle, SB_HORZ, False);

  // operation is provided in the command line
  if wbFindCmdLineParam('OP', s) and (s <> '') then
    for var i: Integer := Low(Procs) to High(Procs) do
      if SameText(s, Procs[i].Title) then begin
        lvProcs.Selected := lvProcs.Items[i];
        bAutoMode := True;
        Break;
      end;

  // select the last used operation if not in automation mode
  if not bAutoMode then begin
    LastUsedProc := Settings.ReadString('Main', 'Operation', Procs[0].ClassName);
    for var i: Integer := Low(Procs) to High(Procs) do
      if Procs[i].Title = LastUsedProc then begin
        lvProcs.Selected := lvProcs.Items[i];
        Break;
      end;

    if lvProcs.ItemIndex = -1 then
      lvProcs.ItemIndex := 0;
  end;

  edInput.Text := Settings.ReadString('Main', 'InputDirectory', ExtractFilePath(Application.ExeName));
  Settings.ReadSectionValues('Input History', edInput.Items);
  for var i := 0 to Pred(edInput.Items.Count) do edInput.Items[i] := edInput.Items.ValueFromIndex[i];

  edOutput.Text := Settings.ReadString('Main', 'OutputDirectory', ExtractFilePath(Application.ExeName));
  Settings.ReadSectionValues('Output History', edOutput.Items);
  for var i := 0 to Pred(edOutput.Items.Count) do edOutput.Items[i] := edOutput.Items.ValueFromIndex[i];

  chkInputSubdir.Checked := Settings.ReadBool('Main', 'InputSubDir', chkInputSubdir.Checked);
  chkSkipOnErrors.Checked := Settings.ReadBool('Main', 'SkipOnErrors', chkSkipOnErrors.Checked);
  chkOutputAll.Checked := Settings.ReadBool('Main', 'OutputAll', chkOutputAll.Checked);
  edPathContains.Text := Settings.ReadString('Main', 'PathContains', edPathContains.Text);
  edThreads.Text := Settings.ReadString('Main', 'Threads', edThreads.Text);

  // overriding with values from the command line if any
  if wbFindCmdLineParam('I', s) and (s <> '') then
    edInput.Text := s;

  if wbFindCmdLineParam('P', s) then
    edPathContains.Text := s;

  if wbFindCmdLineParam('subdir', s) then
    chkInputSubdir.Checked := SameText(s, 'yes');

  if wbFindCmdLineParam('skip', s) then
    chkSkipOnErrors.Checked := SameText(s, 'yes');

  if wbFindCmdLineParam('O', s) and (s <> '') then
    edOutput.Text := s;

  if wbFindCmdLineParam('all', s) then
    chkOutputAll.Checked := SameText(s, 'yes');

  if wbFindCmdLineParam('threads', s) then
    if StrToIntDef(s, -1) >= 0 then
      edThreads.Text := s;

  // if automated then start processing immediately
  if bAutoMode then
    PostMessage(Handle, WM_PROCESSING_START, 0, 0)
end;

procedure TFormMain.FormShow(Sender: TObject);
begin
  // focus on the last used operation if not in automation mode
  if not bAutoMode and Assigned(lvProcs.Selected) then
    lvProcs.Selected.MakeVisible(False);
end;

procedure TFormMain.FormClose(Sender: TObject; var Action: TCloseAction);
begin
  if Assigned(Proc) then
    Proc.OnHide;

  if Assigned(ProcFrame) then
    FreeAndNil(ProcFrame);

  Settings.WriteString('Main', 'Theme', TStyleManager.ActiveStyle.Name);

  Settings.WriteString('Main', 'InputDirectory', edInput.Text);
  Settings.EraseSection('Input History');
  for var i := 0 to Pred(edInput.Items.Count) do
    Settings.WriteString('Input History', 'Item' + IntToStr(i), edInput.Items[i]);

  Settings.WriteString('Main', 'OutputDirectory', edOutput.Text);
  Settings.EraseSection('Output History');
  for var i := 0 to Pred(edOutput.Items.Count) do
    Settings.WriteString('Output History', 'Item' + IntToStr(i), edOutput.Items[i]);

  Settings.WriteString('Main', 'PathContains', edPathContains.Text);
  Settings.WriteString('Main', 'Threads', edThreads.Text);
  Settings.WriteBool('Main', 'InputSubDir', chkInputSubdir.Checked);
  Settings.WriteBool('Main', 'SkipOnErrors', chkSkipOnErrors.Checked);
  Settings.WriteBool('Main', 'OutputAll', chkOutputAll.Checked);
  Settings.WriteString('Main', 'Operation', Procs[lvProcs.ItemIndex].Title);

  for var p: TProcBase in Procs do
    p.Free;

  Manager.Free;

  // don't save settings in automated mode
  if not bAutoMode then
    try Settings.UpdateFile; except end;

  Settings.Free;
end;

procedure TFormMain.lblProcessedFilesTitleClick(Sender: TObject);
begin
  var ext := Proc.ExtensionNames;
  var oldext := ext;

  if InputQuery('Processed files', 'Extensions', ext) then begin
    Proc.ExtensionNames := ext;
    if Proc.ExtensionNames = '' then begin
      Proc.ExtensionNames := oldext;
      Exit;
    end;
  end;

  if Proc.ExtensionNames <> oldext then begin
    lblProcessedFiles.Caption := Proc.ExtensionNames;
    Settings.WriteString(Proc.StorageSection, 'ProcessedFiles', Proc.ExtensionNames);
  end;
end;

procedure TFormMain.lvProcsSelectItem(Sender: TObject; Item: TListItem;
  Selected: Boolean);
begin
  if not Selected then
    Exit;

  if Proc = Procs[Item.Index] then
    Exit;

  if Assigned(Proc) then
    Proc.OnHide;

  if Assigned(ProcFrame) then
    FreeAndNil(ProcFrame);

  Proc := Procs[Item.Index];

  Caption := Proc.Title + ' - ' + sSniffTitle;
  Application.Title := Caption;
  lblSupportedGames.Caption := Proc.SupportedGameNames;

  var ext := Settings.ReadString(Proc.StorageSection, 'ProcessedFiles', Proc.ExtensionNames);
  if not SameText(ext, Proc.ExtensionNames) then
    Proc.ExtensionNames := ext;
  lblProcessedFiles.Caption := Proc.ExtensionNames;

  pnlOutput.ShowCaption := Proc.NoOutput;
  for var i: Integer := 0 to Pred(pnlOutput.ControlCount) do
    pnlOutput.Controls[i].Visible := not Proc.NoOutput;

  edThreads.Enabled := Proc.Threads = 0;

  ProcFrame := Proc.GetFrame(Self);

  if Assigned(ProcFrame) then begin
    ProcFrame.Parent := pnlProcFrame;
    ProcFrame.Align := alClient;
  end;

  Proc.OnShow;
end;

procedure TFormMain.mniStyleClick(Sender: TObject);
begin
  var theme := TMenuItem(Sender).Caption;
  if not TStyleManager.TrySetStyle(theme) then
    Exit;

  for var i := 0 to Pred(mniStyle.Count) do
    mniStyle.Items[i].Checked := mniStyle.Items[i].Caption = theme;
end;

procedure TFormMain.tcMainChange(Sender: TObject);
begin
  if tcMain.TabIndex = 0 then begin
    if Assigned(FrameMessages) then
      FrameMessages.Visible := False;

    pnlOperation.Visible := True;
    pnlInput.Visible := True;
    pnlOutput.Visible := True;
    lvProcs.Selected.MakeVisible(False);
    lvProcs.SetFocus;
    btnProcess.Caption := 'Process';
  end

  else if tcMain.TabIndex = 1 then begin
    pnlOperation.Visible := False;
    pnlInput.Visible := False;
    pnlOutput.Visible := False;
    btnProcess.Caption := 'Save Messages';

    if not Assigned(FrameMessages) then begin
      FrameMessages := TFrameMessages.Create(Self);
      FrameMessages.Parent := Self;
      FrameMessages.Align := alClient;
    end;
    FrameMessages.Visible := True;
    with TFrameMessages(FrameMessages) do begin
      memoMessages.Text := Manager.Messages.Text;
      memoMessages.Visible := Manager.Messages.Count <> 0;
      imgTrash.Visible := Manager.Messages.Count = 0;
      if imgTrash.Visible then
        imgTrash.Align := alClient;
    end;
  end;
end;

procedure TFormMain.btnProcessClick(Sender: TObject);

  procedure AddToHistory(cb: TComboBox);
  begin
    var txt := cb.Text;
    var i := cb.Items.IndexOf(txt);
    if i = 0 then Exit;
    if i > 0 then cb.Items.Delete(i);
    cb.Items.Insert(0, txt);
    cb.ItemIndex := 0;
    while cb.Items.Count > cb.DropDownCount do
      cb.Items.Delete(Pred(cb.Items.Count));
  end;

var
  objs: TArray<TProcessObject>;
  obj: TProcessObject;
  i, Threads: integer;
  s: string;
begin
  if tcMain.TabIndex = 1 then begin
    with TFileSaveDialog.Create(Self) do try
      DefaultFolder := ExtractFilePath(ParamStr(0));
      FileName := 'SniffLog.txt';

      if Execute then
        TFrameMessages(FrameMessages).memoMessages.Lines.SaveToFile(FileName);
    finally
      Free;
    end;

    Exit;
  end;

  // ProcessedFiles is filled when drag&dropping
  if Length(ProcessedFiles) = 0 then begin

    if (Trim(edInput.Text) = '') or not TDirectory.Exists(edInput.Text) then begin
      SniffMessage('Input directory not found');
      Exit;
    end;

    if not Proc.NoOutput then
      if (Trim(edOutput.Text) = '') or not TDirectory.Exists(edOutput.Text) then begin
        SniffMessage('Output directory not found');
        Exit;
      end;

    Manager.InputDirectory := IncludeTrailingPathDelimiter(edInput.Text);
    Manager.OutputDirectory := IncludeTrailingPathDelimiter(edOutput.Text);

    if not bAutoMode then begin
      AddToHistory(edInput);
      AddToHistory(edOutput);
    end;
  end;

  if not bAutoMode and Settings.ReadBool('Main', 'PopupWarning', True) then
    with TTaskDialog.Create(Self) do try
      Text := 'You are about to run:'#13 + Proc.Title;
      FooterText := '"Always Yes" to never show this warning again';
      Caption := Application.Title;
      Flags := [tfUseHiconMain, tfPositionRelativeToWindow, tfAllowDialogCancellation];
      CustomMainIcon := Application.Icon;
      CommonButtons := [tcbYes, tcbNo];
      var b := Buttons.Add;
      b.Caption := 'Always Yes';
      b.ModalResult := mrOk;
      ModalResult := mrOk;

      if not Execute or (ModalResult in [mrNo, mrCancel]) then
        Exit;

      if ModalResult = mrOk then
        Settings.WriteBool('Main', 'PopupWarning', False);
    finally
      Free;
    end;


  Manager.InitializeProcessing(Proc);
  Manager.CopyAll := chkOutputAll.Checked;
  Manager.SkipOnErrors := chkSkipOnErrors.Checked;

  try
    Proc.OnStart;
  except
    on E: Exception do begin
      SniffMessage(E.Message);
      SetLength(ProcessedFiles, 0);
      Exit;
    end;
  end;

  btnProcess.Enabled := False;
  btnExit.Enabled := False;
  Application.ProcessMessages;

  try

    // collecting files for processing if not drag&dropped
    if Length(ProcessedFiles) = 0 then begin
      var so: TSearchOption;

      if chkInputSubdir.Checked then
        so := TSearchOption.soAllDirectories
      else
        so := TSearchOption.soTopDirectoryOnly;

      ProcessedFiles := TDirectory.GetFiles(Manager.InputDirectory, '*.*', so);

      // path contains filter
      var path := Trim(edPathContains.Text);
      if path <> '' then
        for i := High(ProcessedFiles) downto Low(ProcessedFiles) do
          if not ContainsText(Copy(ProcessedFiles[i], Length(Manager.InputDirectory) + 1, Length(ProcessedFiles[i])), path) then
            Delete(ProcessedFiles, i, 1);
    end;

    // creating array of processed objects
    for s in ProcessedFiles do begin
      if not Proc.IsAcceptedFile(s) then
        Continue;

      obj := TProcessObject.Create;
      obj.FileName := Copy(s, Length(Manager.InputDirectory) + 1, Length(s));

      SetLength(objs, Succ(Length(objs)));
      objs[Pred(Length(objs))] := obj;
    end;

    var ProcProcess: TProcessProc :=
      procedure(i: Integer) begin
        Manager.Process(objs[i]);
      end;

    // custom number of threads if operation supports multithreading
    Threads := Proc.Threads;
    if Threads = 0 then begin
      Threads := StrToIntDef(edThreads.Text, 0);
      if Threads > System.CPUCount then
        Threads := CPUCount;
    end;

    var sw := TStopwatch.StartNew;

    // single main thread when debugging
    if DebugHook <> 0 then begin
      for i := Low(objs) to High(objs) do
        ProcProcess(i);
    end else

    // multi threaded
    case wbTaskProgressExecute(
      Self, 'Processing...',
      Low(objs), High(objs), ProcProcess, i, s, Threads
    ) of
      mrAbort: begin
        s := 'Error: "' + objs[i].FileName + ': ' + s;
        Manager.AddMessage(#13#10 + s);
        Exit;
      end;
    end;

    try
      Proc.OnStop;
    except
      // suppress OnStop exceptions, doesn't matter
    end;

    Manager.AddMessage(Format('Done. Updated %d files out of %d, elapsed time %s.', [
      Manager.ModifiedCount,
      Manager.ProcessedCount,
      sw.Elapsed.ToString
    ]));

    // close the app after processing if automated
    if bAutoMode then begin
      // if log file name is provided in the command line then save messages there first
      if wbFindCmdLineParam('LOG', s) and (s <> '') then begin
        if not TPath.IsPathRooted(s) then
          s := TPath.Combine(ExtractFilePath(ParamStr(0)), s);
        try Manager.Messages.SaveToFile(s); except end;
      end;

      Close;
    end;

  finally
    SetLength(ProcessedFiles, 0);
    for i := Low(objs) to High(objs) do
      objs[i].Free;

    btnProcess.Enabled := True;
    btnExit.Enabled := True;

    tcMain.TabIndex := 1;
    tcMainChange(nil);
  end;

end;

procedure TFormMain.btnExitClick(Sender: TObject);
begin
  Close;
end;

end.
