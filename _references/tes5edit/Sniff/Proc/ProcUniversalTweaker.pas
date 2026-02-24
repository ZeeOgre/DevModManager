{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcUniversalTweaker;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor, JsonDataObjects,
  Vcl.StdCtrls, Vcl.ExtCtrls, Vcl.Mask, Vcl.Menus;

type
  TFrameUniversalTweaker = class(TFrame)
    StaticText1: TStaticText;
    edPath: TLabeledEdit;
    edValue: TLabeledEdit;
    Label1: TLabel;
    chkOldValueCheck: TCheckBox;
    cmbOldValueMode: TComboBox;
    edOldValue: TEdit;
    cmbNewValueMode: TComboBox;
    edBlocks: TEdit;
    chkInherited: TCheckBox;
    chkReport: TCheckBox;
    edOldPath: TEdit;
    btnPreset: TButton;
    menuPreset: TPopupMenu;
    miPresetAdd: TMenuItem;
    miPresetRemove: TMenuItem;
    N1: TMenuItem;
    DefaultPresets: TStaticText;
    procedure chkOldValueCheckClick(Sender: TObject);
    procedure edPathChange(Sender: TObject);
    procedure miPresetAddClick(Sender: TObject);
    procedure miPresetRemoveClick(Sender: TObject);
    procedure miPresetClick(Sender: TObject);
    procedure btnPresetClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
    fPresets: TJSONObject;
    fPreset: string;
    function AddPreset(const aPreset: string): TMenuItem;
  end;

  TTweakOldValueMode = (ovmEqual = 0, ovmNotEqual, ovmGreater, ovmLesser,
    ovmContains, ovmDoesntContain, ovmStartsWith, ovmEndsWith,
    ovmAnd, ovmAndNot, ovmRegExp);

  TTweakNewValueMode = (nvmSet = 0, nvmAdd, nvmMul, nvmReplace, nvmPrepend, nvmAppend,
    nvmAnd, nvmAndNot, nvmOr);

  TProcUniversalTweaker = class(TProcBase)
  private
    Frame: TFrameUniversalTweaker;
    fBlocks: array of string;
    fInherited: Boolean;
    fPath: string;
    fValue: string;
    fValueMode: TTweakNewValueMode;
    fOldValueCheck: Boolean;
    fOldPath: string;
    fOldValueMode: TTweakOldValueMode;
    fOldValue: string;
    fReportOnly: Boolean;
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
  System.StrUtils,
  System.Math,
  System.RegularExpressionsCore,
  wbDataFormat,
  wbDataFormatNif,
  wbDataFormatMaterial;

constructor TProcUniversalTweaker.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Universal tweaker';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif', 'kf', 'bgsm', 'bgem'];
end;

function TProcUniversalTweaker.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameUniversalTweaker.Create(aOwner);
  Result := Frame;
end;

procedure TFrameUniversalTweaker.btnPresetClick(Sender: TObject);
var
  pnt: TPoint;
begin
  if GetCursorPos(pnt) then
    menuPreset.Popup(pnt.X, pnt.Y);
end;

procedure TFrameUniversalTweaker.chkOldValueCheckClick(Sender: TObject);
begin
  edOldPath.Enabled := chkOldValueCheck.Checked;
  cmbOldValueMode.Enabled := chkOldValueCheck.Checked;
  edOldValue.Enabled := chkOldValueCheck.Checked;
end;

procedure TFrameUniversalTweaker.edPathChange(Sender: TObject);
begin
  edOldPath.TextHint := edPath.Text;
end;

function TFrameUniversalTweaker.AddPreset(const aPreset: string): TMenuItem;
begin
  Result := TMenuItem.Create(menuPreset);
  Result.Caption := aPreset;
  Result.AutoCheck := True;
  Result.RadioItem := True;
  Result.GroupIndex := 1;
  Result.OnClick := miPresetClick;
  menuPreset.Items.Add(Result);
end;

procedure TFrameUniversalTweaker.miPresetAddClick(Sender: TObject);
var
  s: string;
begin
  if not InputQuery('Universal tweaker', 'Add preset', s) then
    Exit;

  if (s = '') or (s = 'Add') or (s = 'Remove') or (s = '-') then
    Exit;

  fPresets.O[s].S['sBlocks'] := edBlocks.Text;
  fPresets.O[s].B['sDescendants'] := chkInherited.Checked;
  fPresets.O[s].S['sPath'] := edPath.Text;
  fPresets.O[s].I['iValueMode'] := cmbNewValueMode.ItemIndex;
  fPresets.O[s].S['sValue'] := edValue.Text;
  fPresets.O[s].B['bOldValueCheck'] := chkOldValueCheck.Checked;
  fPresets.O[s].S['sOldPath'] := edOldPath.Text;
  fPresets.O[s].I['iOldValueMode'] := cmbOldValueMode.ItemIndex;
  fPresets.O[s].S['sOldValue'] := edOldValue.Text;

  AddPreset(s).Checked := True;
  fPreset := s;
end;

procedure TFrameUniversalTweaker.miPresetRemoveClick(Sender: TObject);
begin
  if fPreset = '' then
    with TTaskDialog.Create(Self) do try
      Text := 'Select any preset first';
      Caption := Application.Title;
      Flags := [tfUseHiconMain, tfPositionRelativeToWindow, tfAllowDialogCancellation];
      CustomMainIcon := Application.Icon;
      CommonButtons := [tcbClose];
      Execute;
      Exit;
    finally
      Free;
    end;

  with TTaskDialog.Create(Self) do try
    Text := 'Remove preset:'#13 + fPreset;
    Caption := Application.Title;
    Flags := [tfUseHiconMain, tfPositionRelativeToWindow, tfAllowDialogCancellation];
    CustomMainIcon := Application.Icon;
    CommonButtons := [tcbYes, tcbNo];
    if not Execute or (ModalResult <> mrYes) then
      Exit;
  finally
    Free;
  end;

  fPresets.Remove(fPreset);
  for var Item in menuPreset.Items do
    if Item.Caption = fPreset then begin
      Item.Free;
      Break;
    end;

  fPreset := '';
end;

procedure TFrameUniversalTweaker.miPresetClick(Sender: TObject);
begin
  var s := TMenuItem(Sender).Caption;
  if not fPresets.Contains(s) then
    Exit;

  edBlocks.Text := fPresets.O[s].S['sBlocks'];
  chkInherited.Checked := fPresets.O[s].B['sDescendants'];
  edPath.Text := fPresets.O[s].S['sPath'];
  edPathChange(nil);
  cmbNewValueMode.ItemIndex := fPresets.O[s].I['iValueMode'];
  edValue.Text := fPresets.O[s].S['sValue'];
  chkOldValueCheck.Checked := fPresets.O[s].B['bOldValueCheck'];
  edOldPath.Text := fPresets.O[s].S['sOldPath'];
  cmbOldValueMode.ItemIndex := fPresets.O[s].I['iOldValueMode'];
  edOldValue.Text := fPresets.O[s].S['sOldValue'];

  fPreset := s;
end;

procedure TProcUniversalTweaker.OnShow;
begin
  Frame.chkReport.Checked := StorageGetBool('bReportOnly', Frame.chkReport.Checked);
  Frame.edBlocks.Text := StorageGetString('sBlocks', Frame.edBlocks.Text);
  Frame.chkInherited.Checked := StorageGetBool('bDescendants', Frame.chkInherited.Checked);
  Frame.edPath.Text := StorageGetString('sPath', Frame.edPath.Text);
  Frame.cmbNewValueMode.ItemIndex := StorageGetInteger('iValueMode', Frame.cmbNewValueMode.ItemIndex);
  Frame.edValue.Text := StorageGetString('sValue', Frame.edValue.Text);
  Frame.edOldPath.Text := StorageGetString('sOldPath', Frame.edOldPath.Text);
  Frame.chkOldValueCheck.Checked := StorageGetBool('bOldValueCheck', Frame.chkOldValueCheck.Checked);
  Frame.cmbOldValueMode.ItemIndex := StorageGetInteger('iOldValueMode', Frame.cmbOldValueMode.ItemIndex);
  Frame.edOldValue.Text := StorageGetString('sOldValue', Frame.edOldValue.Text);
  Frame.chkOldValueCheckClick(nil);
  Frame.fPresets := TJSONObject.Create;
  var s := StorageGetString('sPresets', Frame.DefaultPresets.Caption);
  try Frame.fPresets.FromJSON(s); except end;
  with TStringList.Create do try
    for var i := 0 to Pred(Frame.fPresets.Count) do
      Add(Frame.fPresets.Names[i]);
    Sort;
    for var i := 0 to Pred(Count) do
      Frame.AddPreset(Strings[i]);
  finally
    Free;
  end;
end;

procedure TProcUniversalTweaker.OnHide;
begin
  StorageSetString('sBlocks', Frame.edBlocks.Text);
  StorageSetBool('bDescendants', Frame.chkInherited.Checked);
  StorageSetString('sPath', Frame.edPath.Text);
  StorageSetInteger('iValueMode', Frame.cmbNewValueMode.ItemIndex);
  StorageSetString('sValue', Frame.edValue.Text);
  StorageSetString('sOldPath', Frame.edOldPath.Text);
  StorageSetBool('bOldValueCheck', Frame.chkOldValueCheck.Checked);
  StorageSetInteger('iOldValueMode', Frame.cmbOldValueMode.ItemIndex);
  StorageSetString('sOldValue', Frame.edOldValue.Text);
  StorageSetBool('bReportOnly', Frame.chkReport.Checked);
  StorageSetString('sPresets', Frame.fPresets.ToJSON(True));
  Frame.fPresets.Free;
end;

procedure TProcUniversalTweaker.OnStart;
begin
  with TStringList.Create do try
    Delimiter := ',';
    StrictDelimiter := True;
    DelimitedText := Frame.edBlocks.Text;
    SetLength(fBlocks, Count);
    for var i: Integer := 0 to Pred(Count) do
      fBlocks[i] := Trim(Strings[i]);
  finally
    Free;
  end;

  fInherited := Frame.chkInherited.Checked;

  fPath := Frame.edPath.Text;
  if fPath = '' then
    raise Exception.Create('Field path can not be empty');

  fValueMode := TTweakNewValueMode(Frame.cmbNewValueMode.ItemIndex);
  fValue := Frame.edValue.Text;

  fOldPath := Frame.edOldPath.Text;
  fOldValueCheck := Frame.chkOldValueCheck.Checked;
  fOldValueMode := TTweakOldValueMode(Frame.cmbOldValueMode.ItemIndex);
  fOldValue := Frame.edOldValue.Text;

  if (fValueMode = nvmReplace) and (not fOldValueCheck or not (fOldValueMode in [ovmContains, ovmStartsWith, ovmEndsWith, ovmRegExp]) or (fOldValue = '')) then
    raise Exception.Create('When replacing, if field must be checked using "Contains", "Start with", "Ends with" or "Regular Expr" with non-empty value');

  if fValueMode in [nvmAdd, nvmMul, nvmAnd, nvmAndNot, nvmOr] then try
    dfStrToFloat(fValue);
  except
    raise Exception.Create('Value must be a number');
  end;

  if fOldValueCheck and (fOldValueMode in [ovmGreater, ovmLesser, ovmAnd, ovmAndNot]) then try
    dfStrToFloat(fOldValue);
  except
    raise Exception.Create('Another field''s value must be a number');
  end;

  fReportOnly := Frame.chkReport.Checked;
end;


function ModifyElement(aBlock: TdfElement;
  const aPath, aValue, aOldPath, aOldValue: string;
  aValueMode: TTweakNewValueMode;
  aOldValueCheck: Boolean;
  aOldValueMode: TTweakOldValueMode;
  Log: TStrings;
  regexp: TPerlRegEx
): Boolean;
var
  CurrentValue, NewValue: string;
  FloatCurrentValue, FloatOldValue, FloatNewValue: Extended;
  Matched: Boolean;

  function GetEditValue: string;
  begin
    if aPath <> '' then
      Result := aBlock.EditValues[aPath]
    else
      Result := aBlock.EditValue;
  end;

  function ToFloat: Boolean;
  begin
    Result := True;
    try
      FloatCurrentValue := dfStrToFloat(CurrentValue);
      FloatNewValue := dfStrToFloat(aValue);
      if aOldValueCheck and (aOldValueMode in [ovmEqual, ovmNotEqual, ovmGreater, ovmLesser, ovmAnd, ovmAndNot]) then
        FloatOldValue := dfStrToFloat(aOldValue);
    except
      Result := False;
    end;
  end;

begin
  Result := False;

  // processing all elements in arrays
  var i := Pos('[*]', aPath);
  if i <> 0 then begin
    aBlock := aBlock.Elements[Copy(aPath, 1, i - 2)];
    if not Assigned(aBlock) then
      Exit;
    var p := Copy(aPath, i + 4, Length(aPath));
    for i := 0 to Pred(aBlock.Count) do
      Result := ModifyElement(aBlock[i], p, aValue, aOldPath, aOldValue, aValueMode, aOldValueCheck, aOldValueMode, Log, regexp) or Result;
    Exit;
  end;

  // perform all checks against another element if provided
  if aOldPath <> '' then
    CurrentValue := aBlock.EditValues[aOldPath]
  else
    CurrentValue := GetEditValue;

  Matched := False;

  if aOldValueCheck then
  case aOldValueMode of
    ovmEqual:    Matched := (ToFloat and SameValue(FloatCurrentValue, FloatOldValue)) or SameText(CurrentValue, aOldValue);
    ovmNotEqual: Matched := (ToFloat and not SameValue(FloatCurrentValue, FloatOldValue)) or not SameText(CurrentValue, aOldValue);
    ovmGreater:  Matched := ToFloat and (FloatCurrentValue > FloatOldValue);
    ovmLesser:   Matched := ToFloat and (FloatCurrentValue < FloatOldValue);
    ovmContains: Matched := ContainsText(CurrentValue, aOldValue);
    ovmDoesntContain: Matched := not ContainsText(CurrentValue, aOldValue);
    ovmStartsWith: Matched := CurrentValue.StartsWith(aOldValue, True);
    ovmEndsWith: Matched := CurrentValue.EndsWith(aOldValue, True);
    ovmAnd:      Matched := ToFloat and (Trunc(FloatCurrentValue) and Trunc(FloatOldValue) = Trunc(FloatOldValue));
    ovmAndNot:   Matched := ToFloat and (Trunc(FloatCurrentValue) and Trunc(FloatOldValue) = 0);
    ovmRegExp:   begin
      regexp.Subject := CurrentValue;
      regexp.RegEx := aOldValue;
      regexp.Replacement := aValue;
      Matched := regexp.ReplaceAll;
    end;
  end;

  if not Matched then
    Exit;

  // if we were checking another element, then get the actual value for tweaking
  if aOldPath <> '' then
    CurrentValue := GetEditValue;

  case aValueMode of
    nvmSet:     NewValue := aValue;
    nvmAdd:     if ToFloat then NewValue := dfFloatToStr(FloatCurrentValue + FloatNewValue);
    nvmMul:     if ToFloat then NewValue := dfFloatToStr(FloatCurrentValue * FloatNewValue);
    nvmAnd:     if ToFloat then NewValue := dfFloatToStr(Trunc(FloatCurrentValue) and Trunc(FloatNewValue));
    nvmAndNot:  if ToFloat then NewValue := dfFloatToStr(Trunc(FloatCurrentValue) and not Trunc(FloatNewValue));
    nvmOr:      if ToFloat then NewValue := dfFloatToStr(Trunc(FloatCurrentValue) or Trunc(FloatNewValue));
    nvmReplace: case aOldValueMode of
      ovmContains:   NewValue := StringReplace(CurrentValue, aOldValue, aValue, [rfReplaceAll, rfIgnoreCase]);
      ovmStartsWith: NewValue := aValue + Copy(CurrentValue, Length(aOldValue) + 1, Length(CurrentValue));
      ovmEndsWith:   NewValue := Copy(CurrentValue, 1, Length(CurrentValue) - Length(aOldValue)) + aValue;
      ovmRegExp:     NewValue := regexp.Subject;
    end;
    nvmPrepend: NewValue := aValue + CurrentValue;
    nvmAppend : NewValue := CurrentValue + aValue;
  end;

  // if fractional part is zero (ends with .00000)
  // then remove it in case we are working with int field
  if aValueMode in [nvmAdd, nvmMul, nvmAnd, nvmAndNot, nvmOr] then begin
    var z := Copy(dfFloatToStr(1), 2, 100);
    if NewValue.EndsWith(z) then
      NewValue := Copy(NewValue, 1, Length(NewValue) - Length(z));
  end;

  if aPath <> '' then
    aBlock.EditValues[aPath] := NewValue
  else
    aBlock.EditValue := NewValue;

  Result := CurrentValue <> GetEditValue;
  if Assigned(Log) and Result then begin
    var p := aBlock.Path;
    if aPath <> '' then p := p + '\' + aPath;
    Log.Add(#9 + p + ': Changed from "' + CurrentValue + '" to "' + GetEditValue + '"');
  end;
end;

function TProcUniversalTweaker.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  BGSM: TwbBGSMFile;
  BGEM: TwbBGEMFile;
  Log: TStringList;
  regexp: TPerlRegEx;
  i: Integer;
  block: TwbNifBlock;
  bChanged, bMatched: Boolean;
  ext: string;
begin
  bChanged := False;
  nif := nil; BGSM := nil; BGEM := nil; Log := nil; regexp := nil; // suppress compiler warning

  if fOldValueMode = ovmRegExp then begin
    regexp := TPerlRegEx.Create;
    regexp.Options := [preCaseLess];
  end;

  if fReportOnly then
    Log := TStringList.Create;

  ext := ExtractFileExt(aFileName);
  try
    // *.NIF file
    if SameText(ext, '.nif') or SameText(ext, '.kf') then begin
      nif := TwbNifFile.Create;
      nif.LoadFromFile(aInputDirectory + aFileName);

      // processing specific block by path
      if (Length(fBlocks) = 1) and (Pos('\', fBlocks[0]) <> 0) then begin
        block := nif.BlockByPath(fBlocks[0]);
        if not Assigned(block) then
          Exit;

        bChanged := ModifyElement(block, fPath, fValue, fOldPath, fOldValue, fValueMode, fOldValueCheck, fOldValueMode, Log, regexp);
      end

      else begin
        // if processing BSXFlags and it is missing, then add it
        if (nif.NifVersion >= nfTES4) and (fBlocks[0] = 'BSXFlags') then
          if not Assigned(nif.BlockByType('BSXFlags')) and (nif.BlocksCount <> 0) and nif.RootNode.IsNiObject('NiNode') then
            nif.RootNode.AddExtraData('BSXFlags').EditValues['Name'] := 'BSX';

        // processing blocks by type including NiHeader and NiFooter
        for i := 0 to Pred(nif.Count) do begin
          block := TwbNifBlock(nif[i]);

          bMatched:= False;
          for var s in fBlocks do
            if block.IsNiObject(s, fInherited) then
              bMatched := True;

          if not bMatched and (Length(fBlocks) <> 0) then
            Continue;

          bChanged := ModifyElement(block, fPath, fValue, fOldPath, fOldValue, fValueMode, fOldValueCheck, fOldValueMode, Log, regexp) or bChanged;
        end;
      end;
    end

    // *.BGSM file
    else if SameText(ext, '.bgsm') then begin
      BGSM := TwbBGSMFile.Create;
      BGSM.LoadFromFile(aInputDirectory + aFileName);

      bChanged := ModifyElement(BGSM, fPath, fValue, fOldPath, fOldValue, fValueMode, fOldValueCheck, fOldValueMode, Log, regexp) or bChanged;
    end

    // *.BGEM file
    else if SameText(ext, '.bgem') then begin
      BGEM := TwbBGEMFile.Create;
      BGEM.LoadFromFile(aInputDirectory + aFileName);

      bChanged := ModifyElement(BGEM, fPath, fValue, fOldPath, fOldValue, fValueMode, fOldValueCheck, fOldValueMode, Log, regexp) or bChanged;
    end;

    if bChanged and not fReportOnly then begin
      if Assigned(nif) then nif.SaveToData(Result);
      if Assigned(BGSM) then BGSM.SaveToData(Result);
      if Assigned(BGEM) then BGEM.SaveToData(Result);
    end;

    if bChanged and fReportOnly then begin
      fManager.AddMessage(aFileName);
      fManager.AddMessages(Log);
      fManager.AddMessage('');
    end;

  finally
    if Assigned(nif) then nif.Free;
    if Assigned(BGSM) then BGSM.Free;
    if Assigned(BGEM) then BGEM.Free;
    if Assigned(Log) then Log.Free;
    if Assigned(regexp) then regexp.Free;
  end;

end;

end.
