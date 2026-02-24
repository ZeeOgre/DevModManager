unit ProcUnskinMesh;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, SniffProcessor;

type
  TFrameUnskinMesh = class(TFrame)
    StaticText1: TStaticText;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcUnskinMesh = class(TProcBase)
  private
    Frame: TFrameUnskinMesh;
  public
    constructor Create(aManager: TProcManager); override;
    function GetFrame(aOwner: TComponent): TFrame; override;

    function ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes; override;
  end;


implementation

{$R *.dfm}

uses
  wbDataFormat,
  wbDataFormatNif;

constructor TProcUnskinMesh.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Unskin mesh';
  fSupportedGames := [gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcUnskinMesh.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameUnskinMesh.Create(aOwner);
  Result := Frame;
end;

function TProcUnskinMesh.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  bChanged: Boolean;

  procedure UnSkin(const shape: TwbNifBlock);
  begin
    var skin := shape.GetSkin;
    if not Assigned(skin) then
      Exit;

    skin.RemoveBranch(True);
    bChanged := True;

    var shader := shape.PropertyByType('BSShaderProperty', True);
    if Assigned(shader) then begin
      shader.NativeValues['Shader Flags 1\Skinned'] := False;
      bChanged := True;
    end;

    shape.UpdateBounds;
  end;

begin
  nif := TwbNifFile.Create;
  nif.Options := [nfoCollapseLinkArrays, nfoRemoveUnusedStrings];
  bChanged := False;

  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    for var b in nif.BlocksByType('NiNode') do
      if b.IsBone then begin
        nif.Delete(b.Index);
        bChanged := True;
      end;

    for var b in nif.BlocksByType('NiTriBasedGeom', True) do
      UnSkin(b);

    for var b in nif.BlocksByType('BSTriShape', True) do begin
      UnSkin(b);

      if b.NativeValues['VertexDesc\VF\VF_SKINNED'] then begin
        b.NativeValues['VertexDesc\VF\VF_SKINNED'] := False;
        bChanged := True;
      end;

      if b.BlockType = 'BSSubIndexTriShape' then begin
        nif.ConvertBlock(b.Index, 'BSTriShape');
        bChanged := True;
      end;
    end;

    if bChanged then
      nif.SaveToData(Result);

  finally
    nif.Free;
  end;
end;


end.
