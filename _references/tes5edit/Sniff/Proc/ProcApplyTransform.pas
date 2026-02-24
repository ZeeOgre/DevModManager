{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcApplyTransform;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, SniffProcessor,
  wbDataFormatNif;

type
  TFrameApplyTransform = class(TFrame)
    StaticText1: TStaticText;
    chkSkipSkinned: TCheckBox;
    chkSkipAnimated: TCheckBox;
    chkSkipCollision: TCheckBox;
    chkSkipRoot: TCheckBox;
    chkSkipControllerManager: TCheckBox;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcApplyTransform = class(TProcBase)
  private
    Frame: TFrameApplyTransform;
    fOptions: TwbApplyTransformOptions;
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
  wbDataFormat;
  //wbDataFormatNif;

constructor TProcApplyTransform.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Apply transformation';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcApplyTransform.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameApplyTransform.Create(aOwner);
  Result := Frame;
end;

procedure TProcApplyTransform.OnShow;
begin
  Frame.chkSkipSkinned.Checked := StorageGetBool('bSkipSkinned', Frame.chkSkipSkinned.Checked);
  Frame.chkSkipAnimated.Checked := StorageGetBool('bSkipAnimated', Frame.chkSkipAnimated.Checked);
  Frame.chkSkipCollision.Checked := StorageGetBool('bSkipCollision', Frame.chkSkipCollision.Checked);
  Frame.chkSkipRoot.Checked := StorageGetBool('bSkipRoot', Frame.chkSkipRoot.Checked);
  Frame.chkSkipControllerManager.Checked := StorageGetBool('bSkipControllerManager', Frame.chkSkipControllerManager.Checked);
end;

procedure TProcApplyTransform.OnHide;
begin
  StorageSetBool('bSkipSkinned', Frame.chkSkipSkinned.Checked);
  StorageSetBool('bSkipAnimated', Frame.chkSkipAnimated.Checked);
  StorageSetBool('bSkipCollision', Frame.chkSkipCollision.Checked);
  StorageSetBool('bSkipRoot', Frame.chkSkipRoot.Checked);
  StorageSetBool('bSkipControllerManager', Frame.chkSkipControllerManager.Checked);
end;

procedure TProcApplyTransform.OnStart;
begin
  fOptions := [];
  if not Frame.chkSkipSkinned.Checked then fOptions := fOptions + [atrSkinned];
  if not Frame.chkSkipAnimated.Checked then fOptions := fOptions + [atrAnimated];
  if not Frame.chkSkipCollision.Checked then fOptions := fOptions + [atrCollision];
  if not Frame.chkSkipRoot.Checked then fOptions := fOptions + [atrRoot];
  if not Frame.chkSkipControllerManager.Checked then fOptions := fOptions + [atrCtrlManager];
end;

function TProcApplyTransform.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
begin
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.BlocksCount = 0 then
      Exit;

    if nif.RootNode.ApplyTransform(True, fOptions) then
      nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;

end.
