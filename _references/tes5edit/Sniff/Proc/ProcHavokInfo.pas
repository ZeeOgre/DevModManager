unit ProcHavokInfo;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

type
  TFrameHavokInfo = class(TFrame)
    StaticText1: TStaticText;
    chkPerObject: TCheckBox;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcHavokInfo = class(TProcBase)
  private
    Frame: TFrameHavokInfo;
    fPerObject: Boolean;
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

constructor TProcHavokInfo.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Havok information';
  fSupportedGames := [gtTES4, gtFO3, gtFNV, gtTES5, gtSSE];
  fExtensions := ['nif'];
  fNoOutput := True;
end;

function TProcHavokInfo.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameHavokInfo.Create(aOwner);
  Result := Frame;
end;

procedure TProcHavokInfo.OnShow;
begin
  Frame.chkPerObject.Checked := StorageGetBool('bPerObject', Frame.chkPerObject.Checked);
end;

procedure TProcHavokInfo.OnHide;
begin
  StorageSetBool('bPerObject', Frame.chkPerObject.Checked);
end;

procedure TProcHavokInfo.OnStart;
begin
  fPerObject := Frame.chkPerObject.Checked;
end;

function TProcHavokInfo.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  Log: TStringList;
  statics, dynamics: Integer;
  mass: Single;
begin
  nif := TwbNifFile.Create;
  Log := TStringList.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    statics := 0; dynamics := 0; mass := 0;
    for var col in nif.BlocksByType('bhkCollisionObject', True) do begin
      var rigid := TwbNifBlock(col.Elements['Body'].LinksTo);
      if not Assigned(rigid) then
        Continue;

      var target := TwbNifBlock(col.Elements['Target'].LinksTo);
      var name := '<No target>';
      if Assigned(target) then begin
        name := target.EditValues['Name'];
        if name = '' then
          name := target.Name;
      end else
        name := '<No target>';

      var shape := TwbNifBlock(rigid.Elements['Shape'].LinksTo);
      var shapetype := '<No shape>';
      if Assigned(shape) then
        shapetype := shape.BlockType;


      if fPerObject then
        Log.Add(Format(#9'%s      %s    %s    %s', [name,
          rigid.EditValues['Mass'],
          rigid.EditValues['Havok Filter\Layer'],
          shapetype
        ]));

      if rigid.IsDynamicRigidBody then
        Inc(dynamics)
      else
        Inc(statics);

      mass := mass + rigid.NativeValues['Mass'];
    end;

    Log.Sort;
    if statics + dynamics > 0 then
      Log.Add(Format(#9'Static: %d    Dynamic: %d    Total Mass: %s', [statics, dynamics, dfFloatToStr(mass)]));

    if Log.Count > 0 then begin
      fManager.AddMessage(aFileName);
      Log.Add('');
      fManager.AddMessages(Log);
    end;

  finally
    nif.Free;
    Log.Free;
  end;

end;


end.
