{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcCopyPriorities;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, Vcl.Mask,
  Vcl.ExtCtrls, SniffProcessor;

type
  TFrameCopyPriorities = class(TFrame)
    StaticText1: TStaticText;
    edSourceDirectory: TLabeledEdit;
    btnBrowse: TButton;
    procedure btnBrowseClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcCopyPriorities = class(TProcBase)
  private
    Frame: TFrameCopyPriorities;
    fSourceDirectory: string;
  public
    constructor Create(aManager: TProcManager); override;
    function GetFrame(aOwner: TComponent): TFrame; override;
    procedure OnShow; override;
    procedure OnHide; override;
    procedure OnStart; override;

    function ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes; override;
  end;

implementation

{$R *.dfm}

uses
  wbDataFormat,
  wbDataFormatNif;

procedure TFrameCopyPriorities.btnBrowseClick(Sender: TObject);
var
  path: string;
begin
  path := edSourceDirectory.Text;

  if path = '' then
    path := ExtractFilePath(Application.ExeName);

  if SelectFolder(path) then
    edSourceDirectory.Text := Path;
end;

constructor TProcCopyPriorities.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Copy anim priorities';
  fSupportedGames := [gtTES4, gtFO3, gtFNV];
  fExtensions := ['kf'];
end;

function TProcCopyPriorities.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameCopyPriorities.Create(aOwner);
  Result := Frame;
end;

procedure TProcCopyPriorities.OnShow;
begin
  Frame.edSourceDirectory.Text := StorageGetString('sSourceDirectory', Frame.edSourceDirectory.Text);
end;

procedure TProcCopyPriorities.OnHide;
begin
  StorageSetString('sSourceDirectory', Frame.edSourceDirectory.Text);
end;

procedure TProcCopyPriorities.OnStart;
begin
  fSourceDirectory := Frame.edSourceDirectory.Text;

  if (fSourceDirectory = '') or not DirectoryExists(fSourceDirectory) then
    raise Exception.Create('Source directory not found');

  fSourceDirectory := IncludeTrailingPathDelimiter(fSourceDirectory);
end;

function TProcCopyPriorities.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  Nif, SrcNif: TwbNifFile;
  SrcBlocks, DstBlocks: TdfElement;
  SrcList: TStringList;
  entry: TdfElement;
  i: Integer;
  bChanged: Boolean;
begin
  if not FileExists(fSourceDirectory + aFileName) then
    Exit;

  bChanged := False;

  SrcList := TStringList.Create;
  Nif := TwbNifFile.Create;
  SrcNif := TwbNifFile.Create;
  try
    Nif.LoadFromFile(aInputDirectory + aFileName);
    SrcNif.LoadFromFile(fSourceDirectory + aFileName);

    if (SrcNif.BlocksCount = 0) or (Nif.BlocksCount = 0) then
      Exit;

    SrcBlocks := SrcNif.RootNode.Elements['Controlled Blocks'];
    DstBlocks := Nif.RootNode.Elements['Controlled Blocks'];

    if not Assigned(SrcBlocks) or not Assigned(DstBlocks) then
      Exit;

    // building a list of controlled blocks and priorities in source nif
    for i := 0 to Pred(SrcBlocks.Count) do
      SrcList.AddObject(SrcBlocks[i].EditValues['Node Name'], Pointer(Integer(SrcBlocks[i].NativeValues['Priority'])));

    SrcList.Sorted := True;

    // copying priorities from the source nif
    for i := 0 to Pred(DstBlocks.Count) do begin
      entry := DstBlocks[i];
      var idx := SrcList.IndexOf(entry.EditValues['Node Name']);
      // skip if block with the same name doesn't exists in the source nif
      if idx = -1 then
        Continue;

      // skip if priority is the same
      var p := Integer(SrcList.Objects[idx]);
      if entry.NativeValues['Priority'] = p then
        Continue;

      entry.NativeValues['Priority'] := p;
      bChanged := True;
    end;

    if bChanged then
      Nif.SaveToData(Result);

  finally
    SrcList.Free;
    Nif.Free;
    SrcNif.Free;
  end;

end;

end.
