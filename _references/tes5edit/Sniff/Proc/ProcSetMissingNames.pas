{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcSetMissingNames;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, SniffProcessor;

type
  TFrameSetMissingNames = class(TFrame)
    StaticText1: TStaticText;
    chkRenameRoot: TCheckBox;
    Label1: TLabel;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcSetMissingNames = class(TProcBase)
  private
    Frame: TFrameSetMissingNames;
    fRenameRoot: Boolean;
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

constructor TProcSetMissingNames.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Set missing names';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcSetMissingNames.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameSetMissingNames.Create(aOwner);
  Result := Frame;
end;

procedure TProcSetMissingNames.OnShow;
begin
  Frame.chkRenameRoot.Checked := StorageGetBool('bRenameRoot', Frame.chkRenameRoot.Checked);
end;

procedure TProcSetMissingNames.OnHide;
begin
  StorageSetBool('bRenameRoot', Frame.chkRenameRoot.Checked);
end;

procedure TProcSetMissingNames.OnStart;
begin
  fRenameRoot := Frame.chkRenameRoot.Checked;
end;

function TProcSetMissingNames.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  fname, newname: string;
  i: Integer;
  bChanged: Boolean;
begin
  i := 0;
  bChanged := False;
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.BlocksCount = 0 then
      Exit;

    fname := ChangeFileExt(ExtractFileName(aFileName), '');

    if fRenameRoot and nif.RootNode.IsNiObject('NiAVObject') and not SameText(nif.RootNode.EditValues['Name'], fname) then begin
      nif.RootNode.EditValues['Name'] := nif.GetUniqueName(fname);
      bChanged := True;
    end;

    for var b in nif.BlocksByType('NiAVObject', True) do begin
      var name := b.EditValues['Name'];
      if name <> '' then
        Continue;

      repeat
        newname := fname + ':' + IntToStr(i);
        Inc(i);
      until not Assigned(nif.BlockByName(newname));

      b.EditValues['Name'] := newname;
      bChanged := True;
    end;

    if bChanged then
      nif.SaveToData(Result);

  finally
    nif.Free;
  end;
end;



end.
