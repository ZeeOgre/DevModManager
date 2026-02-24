unit ProcAnalyzeMesh;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, SniffProcessor,
  Vcl.Mask, Vcl.ExtCtrls;

const
  sLinkMetrics = 'https://github.com/zeux/meshoptimizer/tree/master?tab=readme-ov-file#efficiency-analyzers';
  sLinkCache = 'https://zeux.io/2017/07/31/optimal-grid-rendering-is-not-optimal/';

type
  TFrameAnalyzeMesh = class(TFrame)
    StaticText1: TStaticText;
    lblInfoMetrics: TLabel;
    edCacheSize: TLabeledEdit;
    lblInfoCache: TLabel;
    edACMR: TLabeledEdit;
    edATVR: TLabeledEdit;
    chkThreshold: TCheckBox;
    chkPerShape: TCheckBox;
    edVertices: TLabeledEdit;
    procedure lblInfoMetricsClick(Sender: TObject);
    procedure lblInfoCacheClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcAnalyzeMesh = class(TProcBase)
  private
    Frame: TFrameAnalyzeMesh;
    fCacheSize: Integer;
    fPerShape: Boolean;
    fThreshold: Boolean;
    fVertices: Integer;
    fACMR, fATVR: Double;
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
  wbDataFormatNif,
  wbNifMath,
  wbMeshOptimize,
  Math,
  ShellApi;

procedure TFrameAnalyzeMesh.lblInfoCacheClick(Sender: TObject);
begin
  ShellExecute(self.WindowHandle, 'open', sLinkCache, nil, nil, SW_SHOWNORMAL);
end;

procedure TFrameAnalyzeMesh.lblInfoMetricsClick(Sender: TObject);
begin
  ShellExecute(self.WindowHandle, 'open', sLinkMetrics, nil, nil, SW_SHOWNORMAL);
end;

constructor TProcAnalyzeMesh.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Analyze mesh';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
  fNoOutput := True;
end;

function TProcAnalyzeMesh.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameAnalyzeMesh.Create(aOwner);
  Result := Frame;
end;

procedure TProcAnalyzeMesh.OnShow;
begin
  Frame.edCacheSize.Text := StorageGetString('sCacheSize', Frame.edCacheSize.Text);
  Frame.chkPerShape.Checked := StorageGetBool('bPerShape', Frame.chkPerShape.Checked);
  Frame.chkThreshold.Checked := StorageGetBool('bThreshold', Frame.chkThreshold.Checked);
  Frame.edACMR.Text := StorageGetString('sACMR', Frame.edACMR.Text);
  Frame.edATVR.Text := StorageGetString('sATVR', Frame.edATVR.Text);
  Frame.edVertices.Text := StorageGetString('sVertices', Frame.edVertices.Text);
end;

procedure TProcAnalyzeMesh.OnHide;
begin
  StorageSetString('sCacheSize', Frame.edCacheSize.Text);
  StorageSetBool('bPerShape', Frame.chkPerShape.Checked);
  StorageSetBool('bThreshold', Frame.chkThreshold.Checked);
  StorageSetString('sACMR', Frame.edACMR.Text);
  StorageSetString('sATVR', Frame.edATVR.Text);
  StorageSetString('sVertices', Frame.edVertices.Text);
end;

procedure TProcAnalyzeMesh.OnStart;
begin
  fCacheSize := StrToIntDef(Frame.edCacheSize.Text, 0);
  if (fCacheSize = 0) or (fCacheSize > 128) then
    raise Exception.Create('Invalid cache size. Default is 16, max 128.');

  fPerShape := Frame.chkPerShape.Checked;
  fThreshold := Frame.chkThreshold.Checked;
  if Frame.edACMR.Text = '' then fACMR := 0 else fACMR := dfStrToFloat(Frame.edACMR.Text);
  if Frame.edATVR.Text = '' then fATVR := 0 else fATVR := dfStrToFloat(Frame.edATVR.Text);
  if Frame.edVertices.Text = '' then fVertices := 0 else fVertices := StrToIntDef(Frame.edVertices.Text, 0);
end;

function TProcAnalyzeMesh.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  Log: TStringList;
  Stats: array of record
    s_verts, s_tris: Integer;
    s_acmr, s_atvr: Double;
    s_overfetch: Double;
  end;

  procedure Analyze(const aName: string; const tris: TTriangleArray; numverts: Integer; numtris: Integer = -1);
  begin
    if Length(tris) = 0 then
      Exit;

    if numtris = -1 then numtris := Length(tris);
    var indices := Tris2Indices(tris);
    var vc := meshopt_analyzeVertexCache(indices, fCacheSize);
    var vf := meshopt_analyzeVertexFetch(indices);

    if fPerShape then begin
      var _acmr := RoundTo(vc.acmr, -1);
      var _atvr := RoundTo(vc.atvr, -1);
      var _overfetch := RoundTo(vf.overfetch, -1);
      if not fThreshold or ( (_acmr > fACMR) or (_atvr > fATVR) or (numverts > fVertices) ) then
        Log.Add(#9 + aName + ': ' + Format('Vertices: %d    Triangles: %d    ACMR: %.1f    ATVR: %.1f    Overfetch: %.1f', [numverts, numtris, _acmr, _atvr, _overfetch]));
    end;

    SetLength(Stats, Succ(Length(Stats)));
    with Stats[Pred(Length(Stats))] do begin
      s_verts := numverts;
      s_tris := numtris;
      s_acmr := vc.acmr;
      s_atvr := vc.atvr;
      s_overfetch := vf.overfetch;
    end;
  end;

begin
  nif := TwbNifFile.Create;
  Log := TStringList.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    for var i := 0 to Pred(nif.BlocksCount) do begin
      var b := nif.Blocks[i];

      if (b.BlockType = 'NiTriShape') or (b.BlockType = 'NiTriStrips') then begin
        var data := TwbNifBlock(b.Elements['Data'].LinksTo);
        // Skinned shapes are not rendered, they only store vertices. Rendered tris are in skin partitions.
        if Assigned(data) and (b.GetSkin = nil) then
          Analyze(data.Name, data.GetTriangles, data.NativeValues['Num Vertices']);
      end

      else if b.IsNiObject('BSTriShape') then begin
        // FO4 skins don't have partitions
        if (nif.NifVersion >= nfFO4) or (b.GetSkin = nil) then
          Analyze(b.Name, b.GetTriangles, b.NativeValues['Num Vertices']);
      end

      else if b.BlockType = 'NiSkinPartition' then begin
        var Parts := b.Elements['Partitions'];
        for var p := 0 to Pred(Parts.Count) do begin
          var Part := Parts[p];
          Analyze(Part.Path, b.GetTriangles(Part), Part.NativeValues['Num Vertices']{, Part.NativeValues['Num Triangles']});
        end;
      end;
    end;

    // summary for the mesh
    var verts: Integer := 0;
    var tris: Integer := 0;
    var acmr: Double := 0;
    var atvr: Double := 0;
    var overfetch: Double := 0;

    for var s in Stats do begin
      Inc(verts, s.s_verts);
      Inc(tris, s.s_tris);
    end;

    for var s in Stats do begin
      acmr := acmr + s.s_tris / tris * s.s_acmr;
      atvr := atvr + s.s_tris / tris * s.s_atvr;
      overfetch := overfetch + s.s_tris / tris * s.s_overfetch;
    end;

    var _acmr := RoundTo(acmr, -1);
    var _atvr := RoundTo(atvr, -1);
    var _overfetch := RoundTo(overfetch, -1);
    if not fThreshold or ( (_acmr > fACMR) or (_atvr > fATVR) or (verts > fVertices) ) then
      Log.Add(Format(#9'Vertices: %d    Triangles: %d    ACMR: %.1f    ATVR: %.1f    Overfetch: %.1f', [verts, tris, _acmr, _atvr, _overfetch]));

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
