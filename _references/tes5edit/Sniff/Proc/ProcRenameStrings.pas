{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcRenameStrings;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls, Vcl.ExtCtrls, Vcl.Mask;

type
  TFrameRenameStrings = class(TFrame)
    StaticText1: TStaticText;
    chkExactMatch: TCheckBox;
    chkTrim: TCheckBox;
    Label1: TLabel;
    memoPairs: TMemo;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcRenameStrings = class(TProcBase)
  private
    Frame: TFrameRenameStrings;
    fSearch, fReplace: array of string;
    fExactMatch: Boolean;
    fTrim: Boolean;
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

constructor TProcRenameStrings.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Rename strings';
  fSupportedGames := [gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif', 'kf'];
end;

function TProcRenameStrings.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameRenameStrings.Create(aOwner);
  Result := Frame;
end;

procedure TProcRenameStrings.OnShow;
begin
  Frame.memoPairs.Lines.CommaText := StorageGetString('sReplacements', Frame.memoPairs.Lines.CommaText);
  Frame.chkExactMatch.Checked := StorageGetBool('bFullMatch', Frame.chkExactMatch.Checked);
  Frame.chkTrim.Checked := StorageGetBool('bTrim', Frame.chkTrim.Checked);
end;

procedure TProcRenameStrings.OnHide;
begin
  StorageSetString('sReplacements', Frame.memoPairs.Lines.CommaText);
  StorageSetBool('bFullMatch', Frame.chkExactMatch.Checked);
  StorageSetBool('bTrim', Frame.chkTrim.Checked);
end;

procedure TProcRenameStrings.OnStart;
var
  s, r: string;
begin
  fExactMatch := Frame.chkExactMatch.Checked;
  fTrim := Frame.chkTrim.Checked;

  SetLength(fSearch, 0);
  SetLength(fReplace, 0);

  with Frame do
  for var i: integer := 0 to memoPairs.Lines.Count div 2 do begin
    s := memoPairs.Lines[i * 2];
    r := memoPairs.Lines[i * 2 + 1];
    // skip if odd line is empty
    if s <> ''  then begin
      fSearch := fSearch + [s];
      fReplace := fReplace + [r];
    end;
  end;
end;

function TProcRenameStrings.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  entries, entry: TdfElement;
  s1, s2: string;
  i: Integer;
  bChanged: Boolean;
begin
  bChanged := False;
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    entries := nif.Header.Elements['Strings'];

    if not Assigned(entries)then
      Exit;

    for i := 0 to Pred(entries.Count) do begin
      entry := entries[i];
      s1 := entry.EditValue;

      for var k := Low(fSearch) to High(fSearch) do begin

        if fExactMatch then begin

          if SameText(s1, fSearch[k]) then
            s2 := fReplace[k]
          else
            s2 := s1;

        end else
          s2 := StringReplace(s1, fSearch[k], fReplace[k], [rfReplaceAll, rfIgnoreCase]);

        if fTrim then
          s2 := Trim(s2);

        if s1 <> s2 then begin
          entry.EditValue := s2;
          bChanged := True;
          Break;
        end;

      end;

    end;

    if bChanged then
      nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;


end.
