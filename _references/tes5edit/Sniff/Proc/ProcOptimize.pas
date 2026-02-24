{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcOptimize;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

const
  sLinkInfo = 'https://github.com/zeux/meshoptimizer?tab=readme-ov-file#vertex-cache-optimization';

type
  TFrameOptimize = class(TFrame)
    StaticText1: TStaticText;
    chkTriangulate: TCheckBox;
    chkStripify: TCheckBox;
    chkVertexCache: TCheckBox;
    Label1: TLabel;
    Label2: TLabel;
    chkVertexFetch: TCheckBox;
    chkOverdraw: TCheckBox;
    lblInfo: TLabel;
    procedure chkTriangulateClick(Sender: TObject);
    procedure chkStripifyClick(Sender: TObject);
    procedure lblInfoClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcOptimize = class(TProcBase)
  private
    Frame: TFrameOptimize;
    fTriangulate: Boolean;
    fStripify: Boolean;
    fVertexCache: Boolean;
    fOverdraw: Boolean;
    fVertexFetch: Boolean;
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
  ShellApi,
  wbDataFormat,
  wbDataFormatNif;

constructor TProcOptimize.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Optimize mesh';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcOptimize.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameOptimize.Create(aOwner);
  Result := Frame;
end;

procedure TProcOptimize.OnShow;
begin
  Frame.chkTriangulate.Checked := StorageGetBool('bTriangulate', Frame.chkTriangulate.Checked);
  Frame.chkStripify.Checked := StorageGetBool('bStripify', Frame.chkStripify.Checked);
  Frame.chkVertexCache.Checked := StorageGetBool('bVertexCache', Frame.chkVertexCache.Checked);
  Frame.chkOverdraw.Checked := StorageGetBool('bOverdraw', Frame.chkOverdraw.Checked);
  Frame.chkVertexFetch.Checked := StorageGetBool('bVertexFetch', Frame.chkVertexFetch.Checked);
end;

procedure TProcOptimize.OnHide;
begin
  StorageSetBool('bTriangulate', Frame.chkTriangulate.Checked);
  StorageSetBool('bStripify', Frame.chkStripify.Checked);
  StorageSetBool('bVertexCache', Frame.chkVertexCache.Checked);
  StorageSetBool('bOverdraw', Frame.chkOverdraw.Checked);
  StorageSetBool('bVertexFetch', Frame.chkVertexFetch.Checked);
end;

procedure TProcOptimize.OnStart;
begin
  fTriangulate := Frame.chkTriangulate.Checked;
  fStripify := Frame.chkStripify.Checked;
  fVertexCache := Frame.chkVertexCache.Checked;
  fOverdraw := Frame.chkOverdraw.Checked;
  fVertexFetch := Frame.chkVertexFetch.Checked;

  if not (fTriangulate or fStripify or fVertexCache or fOverdraw or fVertexFetch) then
    raise Exception.Create('Select at least one optimization');
end;

procedure TFrameOptimize.chkStripifyClick(Sender: TObject);
begin
  if chkStripify.Checked then chkTriangulate.Checked := False;
end;

procedure TFrameOptimize.chkTriangulateClick(Sender: TObject);
begin
  if chkTriangulate.Checked then chkStripify.Checked := False;
end;

procedure TFrameOptimize.lblInfoClick(Sender: TObject);
begin
  ShellExecute(self.WindowHandle, 'open', sLinkInfo, nil, nil, SW_SHOWNORMAL);
end;

function TProcOptimize.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  Options: TwbMeshOptimizeOptions;
begin
  if fTriangulate then Options := Options + [moTriangulate];
  if fStripify then Options := Options + [moStripify];
  if fVertexCache then Options := Options + [moVertexCache];
  if fOverdraw then Options := Options + [moOverdraw];
  if fVertexFetch then Options := Options + [moVertexFetch];

  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.SpellOptimize(Options) then
      nif.SaveToData(Result);
  finally
    nif.Free;
  end;

end;


end.
