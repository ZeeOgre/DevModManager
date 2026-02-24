unit ProcWallsReflectionFlag;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls, Vcl.Mask, Vcl.ExtCtrls;

type
  TFrameWallsReflectionFlag = class(TFrame)
    StaticText1: TStaticText;
    edMapScale: TLabeledEdit;
    edNormalIntensity: TLabeledEdit;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcWallsReflectionFlag = class(TProcBase)
  private
    Frame: TFrameWallsReflectionFlag;
    fMapScale: string;
    fNormalIntensity: string;
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
  Math,
  wbDataFormat,
  wbDataFormatNif;

constructor TProcWallsReflectionFlag.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Real Time Reflections - NVSE';
  fSupportedGames := [gtFO3, gtFNV];
  fExtensions := ['nif'];
end;

function TProcWallsReflectionFlag.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameWallsReflectionFlag.Create(aOwner);
  Result := Frame;
end;

procedure TProcWallsReflectionFlag.OnShow;
begin
  Frame.edMapScale.Text := StorageGetString('sMapScale', Frame.edMapScale.Text);
  Frame.edNormalIntensity.Text := StorageGetString('sNormalIntensity', Frame.edNormalIntensity.Text);
end;

procedure TProcWallsReflectionFlag.OnHide;
begin
  StorageSetString('sMapScale', Frame.edMapScale.Text);
  StorageSetString('sNormalIntensity', Frame.edNormalIntensity.Text);
end;

procedure TProcWallsReflectionFlag.OnStart;
begin
  fMapScale := Frame.edMapScale.Text;
  if fMapScale <> '' then try
    dfStrToFloat(fMapScale);
  except
    raise Exception.Create('Invalid float number for map scale');
  end;

  fNormalIntensity := Frame.edNormalIntensity.Text;
  if fNormalIntensity <> '' then try
    dfStrToFloat(fNormalIntensity);
  except
    raise Exception.Create('Invalid float number for strength');
  end;
end;

function TProcWallsReflectionFlag.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
const
  cNormalIntensity = 'NormalIntensity';
var
  nif: TwbNifFile;
  bChanged: Boolean;
begin
  nif := TwbNifFile.Create;
  bChanged := False;

  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    for var block in nif.BlocksByType('NiTriBasedGeom', True) do begin

      var shader := block.PropertyByType('BSShaderPPLightingProperty');
      if not Assigned(shader) then
        Continue;

      if not shader.NativeValues['Shader Flags 1\Environment_Mapping'] then
        Continue;

      if shader.NativeValues['Shader Flags 2\Envmap_Light_Fade'] or not shader.NativeValues['Shader Flags 2\Unknown10'] then begin
        shader.NativeValues['Shader Flags 2\Envmap_Light_Fade'] := 0;
        shader.NativeValues['Shader Flags 2\Unknown10'] := 1;
        bChanged := True;
      end;

      if (fMapScale <> '') and not SameValue(dfStrToFloat(shader.EditValues['Environment Map Scale']), dfStrToFloat(fMapScale)) then begin
        shader.EditValues['Environment Map Scale'] := fMapScale;
        bChanged := True;
      end;

      if fNormalIntensity <> '' then begin
        var exdata := block.ExtraDataByName(cNormalIntensity);
        if not Assigned(exdata) then begin
          exdata := block.AddExtraData('NiFloatExtraData');
          exdata.EditValues['Name'] := cNormalIntensity;
          bChanged := True;
        end;

        if not SameValue(dfStrToFloat(exdata.EditValues['Float Data']), dfStrToFloat(fNormalIntensity)) then begin
          exdata.EditValues['Float Data'] := fNormalIntensity;
          bChanged := True;
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
