{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcJamAnim;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

type
  TFrameJamAnim = class(TFrame)
    StaticText1: TStaticText;
    chkAddRotation: TCheckBox;
    chkAddTranslation: TCheckBox;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcJamAnim = class(TProcBase)
  private
    Frame: TFrameJamAnim;
    fAddRotation: boolean;
    fAddTranslation: boolean;
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

constructor TProcJamAnim.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Add NiTransformData';
  fSupportedGames := [gtTES4, gtFO3, gtFNV];
  fExtensions := ['kf'];
end;

function TProcJamAnim.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameJamAnim.Create(aOwner);
  Result := Frame;
end;

procedure TProcJamAnim.OnShow;
begin
  Frame.chkAddRotation.Checked := StorageGetBool('bAddRotation', Frame.chkAddRotation.Checked);
  Frame.chkAddTranslation.Checked := StorageGetBool('bAddTranslation', Frame.chkAddTranslation.Checked);
end;

procedure TProcJamAnim.OnHide;
begin
  StorageSetBool('bAddRotation', Frame.chkAddRotation.Checked);
  StorageSetBool('bAddTranslation', Frame.chkAddTranslation.Checked);
end;

procedure TProcJamAnim.OnStart;
begin
  fAddRotation := Frame.chkAddRotation.Checked;
  fAddTranslation := Frame.chkAddTranslation.Checked;

  if not fAddRotation and not fAddTranslation then
    raise Exception.Create('Need to select at least one option');
end;

function TProcJamAnim.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  interpolator, transfdata: TwbNifBlock;
  key: TdfElement;
  t: string;
  bChanged: Boolean;
begin
  bChanged := False;
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    for interpolator in nif.BlocksByType('NiTransformInterpolator') do begin
      if interpolator.Elements['Data'].LinksTo <> nil then
        Continue;

      transfdata := nif.AddBlock('NiTransformData');
      interpolator.NativeValues['Data'] := transfdata.Index;

      if fAddRotation then begin
        transfdata.NativeValues['Num Rotation Keys'] := 2;
        transfdata.EditValues['Rotation Type'] := 'LINEAR_KEY';

        key := transfdata.Elements['Quaternion Keys'].Add;
        key.EditValues['Value'] := interpolator.EditValues['Transform\Rotation'];

        key := transfdata.Elements['Quaternion Keys'].Add;
        key.NativeValues['Time'] := nif.RootNode.NativeValues['Stop Time'];
        key.EditValues['Value'] := interpolator.EditValues['Transform\Rotation'];
      end;

      if fAddTranslation then begin
        t := interpolator.EditValues['Transform\Translation'];
        if not (t.Contains('Min') or t.Contains('Max') or t.Contains('Inf') or t.Contains('NaN')) then begin
          transfdata.NativeValues['Translations\Num Keys'] := 2;
          transfdata.EditValues['Translations\Interpolation'] := 'LINEAR_KEY';

          key := transfdata.Elements['Translations\Keys'].Add;
          key.EditValues['Value'] := t;

          key := transfdata.Elements['Translations\Keys'].Add;
          key.NativeValues['Time'] := nif.RootNode.NativeValues['Stop Time'];
          key.EditValues['Value'] := t;
        end;
      end;

      bChanged := True;
    end;

    if bChanged then
      nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;

end.
