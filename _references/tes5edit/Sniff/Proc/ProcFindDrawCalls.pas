unit ProcFindDrawCalls;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, Vcl.ExtCtrls,
  SniffProcessor, Vcl.Mask;

type
  TFrameFindDrawCalls = class(TFrame)
    StaticText1: TStaticText;
    edCallsNum: TLabeledEdit;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcFindDrawCalls = class(TProcBase)
  private
    Frame: TFrameFindDrawCalls;
    fCallsNum: Integer;
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

constructor TProcFindDrawCalls.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Find excessive draw calls';
  fSupportedGames := [gtFO3, gtFNV];
  fExtensions := ['nif'];
  fNoOutput := True;
end;

function TProcFindDrawCalls.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameFindDrawCalls.Create(aOwner);
  Result := Frame;
end;

procedure TProcFindDrawCalls.OnShow;
begin
  Frame.edCallsNum.Text := StorageGetString('sCallsNum', Frame.edCallsNum.Text);
end;

procedure TProcFindDrawCalls.OnHide;
begin
  StorageSetString('sCallsNum', Frame.edCallsNum.Text);
end;

procedure TProcFindDrawCalls.OnStart;
begin
  try
    fCallsNum := StrToInt(Frame.edCallsNum.Text);
  except
    raise Exception.Create('Draw calls threshold is not an integer value');
  end;
end;

function TProcFindDrawCalls.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;

  function GetShaderPasses(shader: TwbNifBlock): Integer;
  begin
    Result := 1;

    if not Assigned(shader) then
      Exit;

    if shader.NativeValues['Shader Flags 1\Environment_Mapping'] or
       shader.NativeValues['Shader Flags 1\Eye_Environment_Mapping'] or
       shader.NativeValues['Shader Flags 1\Window_Environment_Mapping']
    then
      Inc(Result);

    if shader.NativeValues['Shader Flags 1\FaceGen'] then
      Inc(Result);
  end;

const
  sEditorMarker = 'EditorMarker';
var
  nif: TwbNifFile;
  Log: TStringList;
  Markers: TList;
  i, shapes, passes, calls, totalcalls: Integer;
  block: TwbNifBlock;
begin
  Log := TStringList.Create;
  nif := TwbNifFile.Create;
  Markers := TList.Create;
  totalcalls := 0;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    // list of geom shapes used for EditorMarker
    for i := 0 to Pred(nif.BlocksCount) do begin
      block := nif.Blocks[i];
      if block.IsNiObject('NiNode') and (block.EditValues['Name'].StartsWith(sEditorMarker, True)) then begin
        for var b in block.ChildrenByType('NiTriBasedGeom', True) do
          Markers.Add(b);
      end;
    end;

    for i := 0 to Pred(nif.BlocksCount) do begin
      block := nif.Blocks[i];

      if Markers.IndexOf(block) <> -1 then
        Continue;

      if block.BlockType = 'NiTriShape' then begin
        shapes := 1;
        passes := GetShaderPasses(block.PropertyByType('BSShaderProperty', True));
      end

      else if block.BlockType = 'NiTriStrips' then begin
        var data := TwbNifBlock(block.Elements['Data'].LinksTo);
        if Assigned(data) then
          shapes := data.NativeValues['Num Strips']
        else
          shapes := 1;

        passes := GetShaderPasses(block.PropertyByType('BSShaderProperty', True));
      end

      else
        Continue;

      calls := shapes * passes;
      Inc(totalcalls, calls);
      Log.Add(#9 + IntToStr(calls) + ' draw calls estimated for ' + Block.Name);
    end;

    if totalcalls > fCallsNum then begin
      fManager.AddMessage(aFileName);
      Log.Add(#9 + IntToStr(totalcalls) + ' draw calls estimated in total');
      Log.Add('');
      fManager.AddMessages(Log);
    end;

  finally
    nif.Free;
    Log.Free;
    Markers.Free;
  end;

end;

end.
