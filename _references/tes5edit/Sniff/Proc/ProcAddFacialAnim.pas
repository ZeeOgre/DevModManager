{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcAddFacialAnim;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

type
  TFrameAddFacialAnim = class(TFrame)
    StaticText1: TStaticText;
    chkRemove: TCheckBox;
    memoMods: TMemo;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TAnimKey = record Time, Value: Double; end;
  TAnimMod = record
    Priority: Integer;
    Modifier: string;
    Keys: TArray<TAnimKey>;
  end;

  TProcAddFacialAnim = class(TProcBase)
  private
    Frame: TFrameAddFacialAnim;
    fRemove: Boolean;
    fMods: TArray<TAnimMod>;
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
  wbDataFormat,
  wbDataFormatNif;

constructor TProcAddFacialAnim.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Add facial anim';
  fSupportedGames := [gtFO3, gtFNV];
  fExtensions := ['kf'];
end;

function TProcAddFacialAnim.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameAddFacialAnim.Create(aOwner);
  Result := Frame;
end;

procedure TProcAddFacialAnim.OnShow;
begin
  Frame.chkRemove.Checked := StorageGetBool('bRemoveExisting', Frame.chkRemove.Checked);
  Frame.memoMods.Text := StringToText(StorageGetString('sMods', Frame.memoMods.Text));
end;

procedure TProcAddFacialAnim.OnHide;
begin
  StorageSetBool('bRemoveExisting', Frame.chkRemove.Checked);
  StorageSetString('sMods', TextToString(Frame.memoMods.Text));
end;

procedure TProcAddFacialAnim.OnStart;
const
  IDs: TArray<String> = [
    'Anger', 'Fear', 'Happy', 'Sad', 'Surprise', 'MoodNeutral',
    'MoodAfraid', 'MoodAnnoyed', 'MoodCocky', 'MoodDrugged',
    'MoodPleasant', 'MoodAngry', 'MoodSad', 'Pained', 'CombatAnger',
    'Aah', 'BigAah', 'BMP', 'ChjSh', 'DST', 'Eee', 'Eh',
    'FV', 'i', 'k', 'N', 'Oh', 'OohQ', 'R', 'Th', 'W',
    'BlinkLeft', 'BlinkRight', 'BrowDownLeft', 'BrowDownRight',
    'BrowInLeft', 'BrowInRight', 'BrowUpLeft', 'BrowUpRight',
    'LookDown', 'LookLeft', 'LookRight', 'LookUp', 'SquintLeft',
    'SquintRight', 'HeadPitch', 'HeadRoll', 'HeadYaw'];
var
  Anim: ^TAnimMod;
  Key: ^TAnimKey;
begin
  fRemove := Frame.chkRemove.Checked;
  SetLength(fMods, 0);

  for var i := 0 to Pred(Frame.memoMods.Lines.Count) do begin
    var line := Frame.memoMods.Lines[i];
    if line = '' then
      Continue;

    var vals := SplitString(line, ' ');
    if Length(vals) < 4 then
      raise Exception.CreateFmt('Line %d has less than 4 space separated values', [i+1]);

    SetLength(fMods, Succ(Length(fMods)));
    Anim := @fMods[Pred(Length(fMods))];

    Anim.Priority := StrToIntDef(vals[Low(vals)], -1);
    if Anim.Priority = -1 then
      raise Exception.CreateFmt('Invalid priority "%s" on line %d', [vals[Low(vals)], i+1]);

    Anim.Modifier := vals[Low(vals) + 1];
    var bValidMod := false;
    for var id in IDs do
      if id = Anim.Modifier then bValidMod := True;
    if not bValidMod then
      raise Exception.CreateFmt('Invalid expression/phoneme/modifier "%s" on line %d', [Anim.Modifier, i+1]);

    var j: Integer := Low(vals) + 2;
    while Length(vals) >= j + 2 do begin
      SetLength(Anim.Keys, Succ(Length(Anim.Keys)));
      Key := @Anim.Keys[Pred(Length(Anim.Keys))];

      try
        Key.Time := dfStrToFloat(vals[j]);
      except on E: Exception do
        raise Exception.CreateFmt('Invalid time "%s" on line %d', [vals[j], i+1]);
      end;

      try
        Key.Value := dfStrToFloat(vals[j + 1]);
      except on E: Exception do
        raise Exception.CreateFmt('Invalid intensity "%s" on line %d', [vals[j + 1], i+1]);
      end;

      Inc(j, 2);
    end;

    if Length(Anim.Keys) = 0 then
      raise Exception.CreateFmt('Modifier "%s" has no Time/Intensity values on line %d', [Anim.Modifier, i]);
  end;

end;

function TProcAddFacialAnim.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
const
  sHeadAnims = 'HeadAnims';
  sHeadAnims0 = 'HeadAnims:0';
var
  nif: TwbNifFile;
  seq, interpolator, idata: TwbNifBlock;
  entries, entry, datakeys, datakey: TdfElement;
begin
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.BlocksCount = 0 then
      Exit;

    seq := nif.RootNode;
    if seq.BlockType <> 'NiControllerSequence' then
      Exit;

    entries := seq.Elements['Controlled Blocks'];
    if not Assigned(entries)then
      Exit;

    var bHasHeadAnim := False;
    for var i := Pred(entries.Count) downto 0 do begin
      entry := entries[i];
      // checking for HeadAnim block
      if entry.EditValues['Node Name'] = sHeadAnims then
        bHasHeadAnim := True;
      // remove existing HeadAnims
      if fRemove and (entry.EditValues['Node Name'] = sHeadAnims0) then begin
        interpolator := TwbNifBlock(entry.Elements['Interpolator'].LinksTo);
        if Assigned(interpolator) then begin
          entry.NativeValues['Interpolator'] := -1;
          interpolator.RemoveBranch;
        end;
        entries.Delete(i);
      end;
    end;

    // add HeadAnims block if missing
    if not bHasHeadAnim then begin
      entry := entries.Add;
      entry.EditValues['Node Name'] := sHeadAnims;
      entry.EditValues['Controller Type'] := 'NiVisController';
      interpolator := nif.AddBlock('NiBoolInterpolator');
      interpolator.EditValues['Value'] := 'yes';
      entry.NativeValues['Interpolator'] := interpolator.Index;
      if Length(fMods) > 0 then
        entry.NativeValues['Priority'] := fMods[Low(fMods)].Priority;
    end;

    // add facial anim blocks
    for var Anim in fMods do begin
      entry := entries.Add;
      entry.EditValues['Node Name'] := sHeadAnims0;
      entry.NativeValues['Priority'] := Anim.Priority;
      entry.EditValues['Controller Type'] := 'NiGeomMorpherController';
      entry.EditValues['Interpolator ID'] := Anim.Modifier;

      interpolator := nif.AddBlock('NiFloatInterpolator');
      interpolator.NativeValues['Value'] := 0;
      entry.NativeValues['Interpolator'] := interpolator.Index;

      idata := nif.AddBlock('NiFloatData');
      interpolator.NativeValues['Data'] := idata.Index;
      // setting num keys first because Interpolation fied is disabled when keys = 0
      idata.NativeValues['Data\Num Keys'] := Length(Anim.Keys);
      idata.EditValues['Data\Interpolation'] := 'QUADRATIC_KEY';
      datakeys := idata.Elements['Data\Keys'];
      for var Key in Anim.Keys do begin
        datakey := datakeys.Add;
        datakey.NativeValues['Time'] := Key.Time;
        datakey.NativeValues['Value'] := Key.Value;
      end;
    end;

    seq.NativeValues['Num Controlled Blocks'] := entries.Count;

    nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;


end.
