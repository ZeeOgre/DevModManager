unit ProcFindUVs;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, Vcl.Mask,
  Vcl.ExtCtrls, SniffProcessor;

type
  TFrameFindUVs = class(TFrame)
    StaticText1: TStaticText;
    edUMin: TLabeledEdit;
    edUMax: TLabeledEdit;
    edVMin: TLabeledEdit;
    edVMax: TLabeledEdit;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcFindUVs = class(TProcBase)
  private
    Frame: TFrameFindUVs;
    fUmin, fUmax, fVmin, fVmax: string;
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

constructor TProcFindUVs.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Find UVs';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
  fNoOutput := True;
end;

function TProcFindUVs.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameFindUVs.Create(aOwner);
  Result := Frame;
end;

procedure TProcFindUVs.OnShow;
begin
  Frame.edUMin.Text := StorageGetString('sUMin', Frame.edUMin.Text);
  Frame.edUMax.Text := StorageGetString('sUMax', Frame.edUMax.Text);
  Frame.edVMin.Text := StorageGetString('sVMin', Frame.edVMin.Text);
  Frame.edVMax.Text := StorageGetString('sVMax', Frame.edVMax.Text);
end;

procedure TProcFindUVs.OnHide;
begin
  StorageSetString('sUMin', Frame.edUMin.Text);
  StorageSetString('sUMax', Frame.edUMax.Text);
  StorageSetString('sVMin', Frame.edVMin.Text);
  StorageSetString('sVMax', Frame.edVMax.Text);
end;

procedure TProcFindUVs.OnStart;
begin
  fUmin := Trim(Frame.edUMin.Text);
  fUmax := Trim(Frame.edUMax.Text);
  fVmin := Trim(Frame.edVMin.Text);
  fVmax := Trim(Frame.edVMax.Text);

  if (fUmin = '') and (fUmax = '') and (fVmin = '') and (fVmax = '') then
    raise Exception.Create('Fill in at least one value');

  try
    if fUmin <> '' then StrToFloat(fUmin);
    if fUmax <> '' then StrToFloat(fUmax);
    if fVmin <> '' then StrToFloat(fVmin);
    if fVmax <> '' then StrToFloat(fVmax);
  except
    raise Exception.Create('Invalid float value');
  end;
end;

function TProcFindUVs.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  Log: TStringList;
  Umin, Umax, Vmin, Vmax: Extended;
  Xmin, Xmax, Ymin, Ymax: Double;
begin
  Umin := StrToFloatDef(fUmin, 0);
  Umax := StrToFloatDef(fUmax, 0);
  Vmin := StrToFloatDef(fVmin, 0);
  Vmax := StrToFloatDef(fVmax, 0);

  Log := TStringList.Create;
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    for var i := 0 to Pred(nif.BlocksCount) do begin
      var b := nif.Blocks[i];
      var uvs := b.GetTexCoord;

      if Length(uvs) = 0 then
        Continue;

      Xmin := Umin; Xmax := Umax; Ymin := Vmin; Ymax := Vmax;
      for var uv in uvs do begin
        if (fUMin <> '') and (uv.x < Xmin) then Xmin := uv.x;
        if (fUMax <> '') and (uv.x > Xmax) then Xmax := uv.x;
        if (fVMin <> '') and (uv.y < Ymin) then Ymin := uv.y;
        if (fVMax <> '') and (uv.y > Ymax) then Ymax := uv.y;
      end;

      //Log.Add(#9 + b.Name + ': Found NaN, Min, Max or Inf invalid float');

      var s := '';
      if (fUMin <> '') and (Xmin < Umin) then s := s + 'Umin: ' + dfFloatToStr(Xmin) + #9;
      if (fVMin <> '') and (Ymin < Vmin) then s := s + 'Vmin: ' + dfFloatToStr(Ymin) + #9;
      if (fUMax <> '') and (Xmax > Umax) then s := s + 'Umax: ' + dfFloatToStr(Xmax) + #9;
      if (fVMax <> '') and (Ymax > Vmax) then s := s + 'Vmax: ' + dfFloatToStr(Ymax) + #9;
      if s <> '' then
        Log.Add(#9 + b.Name + ': ' + Trim(s));

    end;

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
