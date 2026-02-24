{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcReplaceAssets;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

type
  TFrameReplaceAssets = class(TFrame)
    StaticText1: TStaticText;
    memoPairs: TMemo;
    Label1: TLabel;
    chkFixAbsolute: TCheckBox;
    chkReport: TCheckBox;
    chkRegExp: TCheckBox;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcReplaceAssets = class(TProcBase)
  private
    Frame: TFrameReplaceAssets;
    fFixAbsolute: Boolean;
    fRegExp: Boolean;
    fReportOnly: Boolean;
    fSearch, fReplace: array of string;
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
  System.NetEncoding,
  System.RegularExpressionsCore,
  wbDataFormat,
  wbDataFormatNif,
  wbDataFormatMaterial;

constructor TProcReplaceAssets.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Search and replace assets';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif', 'bgsm', 'bgem'];
end;

function TProcReplaceAssets.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameReplaceAssets.Create(aOwner);
  Result := Frame;
end;

procedure TProcReplaceAssets.OnShow;
begin
  Frame.chkFixAbsolute.Checked := StorageGetBool('bFixAbsolute', Frame.chkFixAbsolute.Checked);
  Frame.chkRegExp.Checked := StorageGetBool('bRegExp', Frame.chkRegExp.Checked);
  Frame.chkReport.Checked := StorageGetBool('bReportOnly', Frame.chkReport.Checked);
  var s := StorageGetString('sReplacements', '');
  if s <> '' then
    Frame.memoPairs.Lines.Text := TBase64Encoding.Base64String.Decode(s);
end;

procedure TProcReplaceAssets.OnHide;
begin
  StorageSetBool('bFixAbsolute', Frame.chkFixAbsolute.Checked);
  StorageSetBool('bRegExp', Frame.chkRegExp.Checked);
  StorageSetBool('bReportOnly', Frame.chkReport.Checked);
  StorageSetString('sReplacements', TBase64Encoding.Base64String.Encode(Frame.memoPairs.Lines.Text));
end;

procedure TProcReplaceAssets.OnStart;
var
  s, r: string;
begin
  fFixAbsolute := Frame.chkFixAbsolute.Checked;
  fReportOnly := Frame.chkReport.Checked;
  fRegExp := Frame.chkRegExp.Checked;
  fNoOutput := fReportOnly;

  SetLength(fSearch, 0);
  SetLength(fReplace, 0);

  with Frame do
  for var i: integer := 0 to memoPairs.Lines.Count div 2 do begin
    s := memoPairs.Lines[i * 2];
    r := memoPairs.Lines[i * 2 + 1];
    // skip if both odd and even lines are empty
    if (s <> '') or (r <> '') then begin
      fSearch := fSearch + [s];
      fReplace := fReplace + [r];
    end;
  end;

end;

function TProcReplaceAssets.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  i, k, p: integer;
  Elements: TList;
  el: TdfElement;
  Log: TStringList;
  Nif: TwbNifFile;
  BGSM: TwbBGSMFile;
  BGEM: TwbBGEMFile;
  regexp: TPerlRegEx;
  ext, s, s2: string;
  bChanged: Boolean;
begin
  Nif := nil; BGSM := nil; BGEM := nil; regexp := nil; // suppress compiler warning
  Elements := TList.Create;
  Log := TStringList.Create;
  if fRegExp then begin
    regexp := TPerlRegEx.Create;
    regexp.Options := [preCaseLess];
  end;
  bChanged := False;

  ext := ExtractFileExt(aFileName);

  // collecting elements with assets
  try
    // *.NIF file
    if SameText(ext, '.nif') then begin
      Nif := TwbNifFile.Create;
      Nif.LoadFromFile(aInputDirectory + aFileName);
      for el in Nif.GetAssets do
        Elements.Add(el);
    end

    // *.BGSM file
    else if SameText(ext, '.bgsm') then begin
      BGSM := TwbBGSMFile.Create;
      BGSM.LoadFromFile(aInputDirectory + aFileName);
      el := BGSM.Elements['Textures'];
      for i := 0 to Pred(el.Count) do
        Elements.Add(el[i]);
    end

    // *.BGEM file
    else if SameText(ext, '.bgem') then begin
      BGEM := TwbBGEMFile.Create;
      BGEM.LoadFromFile(aInputDirectory + aFileName);
      el := BGEM.Elements['Textures'];
      for i := 0 to Pred(el.Count) do
        Elements.Add(el[i]);
    end;

    // skip to the next file if nothing was found
    if Elements.Count = 0 then
      Exit;

    // do text replacement in collected elements
    el := nil; // suppress compiler warning
    for i := 0 to Pred(Elements.Count) do begin
      if not Assigned(Elements[i]) then
        Continue
      else
        el := TdfElement(Elements[i]);

      // getting file name stored in element
      s := el.EditValue;
      // skip to the next element if empty
      if s = '' then Continue;

      // perform replacements, trim whitespaces just in case
      s2 := Trim(s);
      for k := Low(fSearch) to High(fSearch) do begin
        if fSearch[k] <> '' then begin
          // replace if text to find is not empty
          if not fRegExp then
            s2 := StringReplace(s2, fSearch[k], fReplace[k], [rfIgnoreCase, rfReplaceAll])
          else begin
            regexp.Subject := s2;
            regexp.RegEx := fSearch[k];
            regexp.Replacement := fReplace[k];
            regexp.ReplaceAll;
            s2 := regexp.Subject;
          end;
        end else
          // prepend if empty
          s2 := fReplace[k] + s2;
      end;

      if fFixAbsolute then
        // detect an absolute path
        if (Length(s2) > 2) and (s2[2] = ':') then begin
          // remove path up to Data including it
          p := Pos('\data\', LowerCase(s2));
          if p <> 0 then
            s2 := Copy(s2, p + 6, Length(s2));
          // remove path up to Data Files including it for Morrowind
          if p = 0 then begin
            p := Pos('\data files\', LowerCase(s2));
            if p <> 0 then
              s2 := Copy(s, p + 12, Length(s2));
          end;
        end;

      // if element's value has changed
      if s <> s2 then begin
        // store it
        el.EditValue := s2;

        // report
        if not bChanged then
          Log.Add(#13#10 + aFileName);

        Log.Add(#9 + el.Path + #13#10#9#9'"' + s + '"'#13#10#9#9'"' + el.EditValue + '"');

        // mark file to be saved
        bChanged := True;
      end;

    end;

    // save the file if changed and not in report only mode
    if bChanged and not fReportOnly then
      el.Root.SaveToData(Result);

    fManager.AddMessages(Log);

  finally
    Elements.Free;
    Log.Free;
    if fRegExp then regexp.Free;
    if Assigned(Nif) then Nif.Free;
    if Assigned(BGSM) then BGSM.Free;
    if Assigned(BGEM) then BGEM.Free;
  end;

end;

end.
