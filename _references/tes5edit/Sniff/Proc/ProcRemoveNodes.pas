{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcRemoveNodes;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls, Vcl.ExtCtrls, Vcl.Mask;

type
  TFrameRemoveNodes = class(TFrame)
    StaticText1: TStaticText;
    edNames: TLabeledEdit;
    chkExactMatch: TCheckBox;
    rbName: TRadioButton;
    rbType: TRadioButton;
    cmbType: TComboBox;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcRemoveNodes = class(TProcBase)
  private
    Frame: TFrameRemoveNodes;
    fMode: Integer;
    fNames: array of string;
    fExactMatch: Boolean;
    fType: string;
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
  StrUtils,
  wbDataFormat,
  wbDataFormatNif;

constructor TProcRemoveNodes.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Remove nodes';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif', 'kf'];
end;

function TProcRemoveNodes.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameRemoveNodes.Create(aOwner);
  Result := Frame;
end;

procedure TProcRemoveNodes.OnShow;
var
  m: Integer;
  sl: TStringList;
begin
  Frame.edNames.Text := StorageGetString('sNames', Frame.edNames.Text);
  Frame.chkExactMatch.Checked := StorageGetBool('bExactMatch', Frame.chkExactMatch.Checked);
  m := StorageGetInteger('iMode', 1);
  if m = 1 then Frame.rbName.Checked := True
    else if m = 2 then Frame.rbType.Checked := True
      else Frame.rbName.Checked := True;

  sl := TStringList.Create;
  for var s in wbNiObjectList do
    sl.Add(s);
  sl.Sort;
  Frame.cmbType.Items.Assign(sl);
  sl.Free;

  m := Frame.cmbType.Items.IndexOf(StorageGetString('sType', Frame.cmbType.Text));
  if m = -1 then m := 0;
  Frame.cmbType.ItemIndex := m;
end;

procedure TProcRemoveNodes.OnHide;
var
  m: Integer;
begin
  StorageSetString('sNames', Frame.edNames.Text);
  StorageSetBool('bExactMatch', Frame.chkExactMatch.Checked);
  if Frame.rbName.Checked then m := 1
    else if Frame.rbType.Checked then m := 2
      else m := 1;
  StorageSetInteger('iMode', m);
  StorageSetString('sType', Frame.cmbType.Text);
end;

procedure TProcRemoveNodes.OnStart;
begin
  if Frame.rbName.Checked then fMode := 1
    else if Frame.rbType.Checked then fMode := 2;

  if fMode = 1 then
    with TStringList.Create do try
      Delimiter := ',';
      StrictDelimiter := True;
      DelimitedText := Frame.edNames.Text;
      if Count = 0 then
        raise Exception.Create('Set the node name(s) to remove');
      SetLength(fNames, Count);
      for var i: Integer := 0 to Pred(Count) do
        fNames[i] := Trim(Strings[i]);
    finally
      Free;
    end;

  fExactMatch := Frame.chkExactMatch.Checked;
  fType := Frame.cmbType.Text;
end;

function TProcRemoveNodes.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  block, removeblock: TwbNifBlock;
  name: string;
  bChanged, bFound: Boolean;

  procedure UnlinkBlock(aBlock: TwbNifBlock);
  begin
    for var i := 0 to Pred(nif.BlocksCount) do begin
      var b := nif.Blocks[i];
      for var j := 0 to Pred(b.RefsCount) do
        if TwbNifBlock(b.Refs[j].LinksTo) = aBlock then
          b.Refs[j].NativeValue := -1;
    end;
  end;

  begin
  bChanged := False;
  nif := TwbNifFile.Create;
  nif.Options := [nfoCollapseLinkArrays, nfoRemoveUnusedStrings];
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    // removing blocks will shift indexes, so iterate over the nif until nothing left to remove
    repeat
      bFound := False;

      // loop from 1 to skip root node
      for var i: Integer := 0 to Pred(nif.BlocksCount) do begin
        block := nif.Blocks[i];
        removeblock := nil;

        if fMode = 1 then begin
          name := block.EditValues['Name'];
          for var s: String in fNames do
            if ( fExactMatch and SameText(name, s) ) or ( not fExactMatch and ContainsText(name, s) ) then begin
              removeblock := block;;
              Break;
            end;
        end

        else if fMode = 2 then
         if block.IsNiObject(fType) then
           removeblock := block;

        if not Assigned(removeblock) then
          Continue;

        UnlinkBlock(removeblock);
        removeblock.RemoveBranch;
        bChanged := True;
        bFound := True;
        Break;
      end;

    until not bFound;

    if bChanged then
      nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;

end.
