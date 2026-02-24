{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcConvertRootNode;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

type
  TFrameConvertRootNode = class(TFrame)
    StaticText1: TStaticText;
    cmbNodeFrom: TComboBox;
    Label1: TLabel;
    Label2: TLabel;
    cmbNodeTo: TComboBox;
    chkRoot: TCheckBox;
    procedure cmbNodeFromSelect(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcConvertRootNode = class(TProcBase)
  private
    Frame: TFrameConvertRootNode;
    fNodeFrom: string;
    fNodeTo: string;
    fRoot: Boolean;
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

constructor TProcConvertRootNode.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Convert block type';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcConvertRootNode.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameConvertRootNode.Create(aOwner);
  Result := Frame;
end;

procedure TFrameConvertRootNode.cmbNodeFromSelect(Sender: TObject);
begin
  var n := cmbNodeFrom.Text;
  var s: string;
  if n = 'NiNode'                     then s := 'BSFadeNode,BSLeafAnimNode' else
  if n = 'BSFadeNode'                 then s := 'NiNode,BSLeafAnimNode' else
  if n = 'BSLeafAnimNode'             then s := 'NiNode,BSFadeNode' else
  if n = 'bhkConvexListShape'         then s := 'bhkListShape' else
  if n = 'BSShaderPPLightingProperty' then s := 'Lighting30ShaderProperty' else
  if n = 'NiSkinInstance'             then s := 'BSDismemberSkinInstance';

  cmbNodeTo.Items.CommaText := s;
  cmbNodeTo.ItemIndex := 0;
end;

procedure TProcConvertRootNode.OnShow;
var
  i: Integer;
begin
  Frame.cmbNodeFrom.Items.CommaText := 'NiNode,BSFadeNode,BSLeafAnimNode,bhkConvexListShape,BSShaderPPLightingProperty,NiSkinInstance';

  i := Frame.cmbNodeFrom.Items.IndexOf(StorageGetString('sNodeFrom', Frame.cmbNodeFrom.Text));
  if i = -1 then i := 0;
  Frame.cmbNodeFrom.ItemIndex := i;
  Frame.cmbNodeFromSelect(nil);

  i := Frame.cmbNodeTo.Items.IndexOf(StorageGetString('sNodeTo', Frame.cmbNodeTo.Text));
  if i = -1 then i := 0;
  Frame.cmbNodeTo.ItemIndex := i;

  Frame.chkRoot.Checked := StorageGetBool('bRoot', Frame.chkRoot.Checked);
end;

procedure TProcConvertRootNode.OnHide;
begin
  StorageSetString('sNodeFrom', Frame.cmbNodeFrom.Text);
  StorageSetString('sNodeTo', Frame.cmbNodeTo.Text);
  StorageSetBool('bRoot', Frame.chkRoot.Checked);
end;

procedure TProcConvertRootNode.OnStart;
begin
  fNodeFrom := Frame.cmbNodeFrom.Text;
  fNodeTo := Frame.cmbNodeTo.Text;
  fRoot := Frame.chkRoot.Checked;
end;

function TProcConvertRootNode.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  bChanged: Boolean;
begin
  bChanged := False;
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    var blocks: TwbNifBlocks;
    if not fRoot then
      blocks := nif.BlocksByType(fNodeFrom)
    else if Assigned(nif.RootNode) then
      if nif.RootNode.BlockType = fNodeFrom then
        blocks := [nif.RootNode];

    for var b in blocks do begin
      var i := b.Index;
      nif.ConvertBlock(i, fNodeTo);

      // post convertion updates
      var block := nif.Blocks[i];

      if block.BlockType = 'bhkListShape' then
        block.Elements['Unknown Ints'].Count := 2

      else if block.BlockType = 'Lighting30ShaderProperty' then
        block.EditValues['Shader Type'] := 'SHADER_LIGHTING30'

      else if block.BlockType = 'BSDismemberSkinInstance' then begin
        var parts := 1;
        var skinpart := TwbNifBlock(block.Elements['Skin Partition'].LinksTo);
        if Assigned(skinpart) then
          parts := skinpart.NativeValues['Num Partitions'];
        block.Elements['Partitions'].Count := parts;
        //for var j := 0 to Pred(parts) do
        //  block.Elements['Partitions'].Add;
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
