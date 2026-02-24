{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcAddRootCollisionNode;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, SniffProcessor;

type
  TFrameAddRootCollisionNode = class(TFrame)
    StaticText1: TStaticText;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcAddRootCollisionNode = class(TProcBase)
  private
    Frame: TFrameAddRootCollisionNode;
  public
    constructor Create(aManager: TProcManager); override;
    function GetFrame(aOwner: TComponent): TFrame; override;

    function ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes; override;
  end;


implementation

{$R *.dfm}

uses
  wbDataFormat,
  wbDataFormatNif,
  wbNifMath;

constructor TProcAddRootCollisionNode.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Add RootCollisionNode';
  fSupportedGames := [gtTES3];
  fExtensions := ['nif'];
end;

function TProcAddRootCollisionNode.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameAddRootCollisionNode.Create(aOwner);
  Result := Frame;
end;


type
  TShapeData = record
    Data: TwbNifBlock;
    Transform: TTransform;
  end;
  TShapeDatas = array of TShapeData;

procedure CollectShapeDatas(aBlock: TwbNifBlock; aTransform: TTransform; var aDatas: TShapeDatas);
var
  t: TTransform;
  children: TdfElement;
begin
  if not Assigned(aBlock) then
    Exit;

  // don't use root node transform
  if (aBlock.Index <> 0) and aBlock.GetTransform(t) then
    aTransform := aTransform * t;

  if aBlock.BlockType = 'NiTriShape' then begin
    if aBlock.Hidden then
      Exit;

    var shapedata := TwbNifBlock(aBlock.Elements['Data'].LinksTo);
    if not Assigned(shapedata) then
      Exit;

    if (shapedata.NativeValues['Num Vertices'] = 0) or (shapedata.NativeValues['Num Triangles'] = 0) then
      Exit;

    // Check that we don't already have that shapedata in the list just in case.
    // Happens when different NiNodes link to the same shape.
    for var d in aDatas do
      if d.Data = shapedata then
        Exit;

    SetLength(aDatas, Succ(Length(aDatas)));
    with aDatas[Pred(Length(aDatas))] do begin
      Transform := aTransform;
      Data := shapedata;
    end;
  end

  // iterate over children recursively
  else begin
    children := aBlock.Elements['Children'];
    if not Assigned(children) then
      Exit;

    for var i := 0 to Pred(children.Count) do
      CollectShapeDatas(TwbNifBlock(children[i].LinksTo), aTransform, aDatas);
  end;

end;

function TProcAddRootCollisionNode.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  root, rc, trishape, trishapedata: TwbNifBlock;
  Datas: TShapeDatas;
  t: TTransform;
begin
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.BlocksCount = 0 then
      Exit;

    if Assigned(nif.BlocksByType('RootCollisionNode')) then
      Exit;

    root := nif.RootNode;
    if root.BlockType <> 'NiNode' then
      Exit;

    t.SetNone;
    CollectShapeDatas(root, t, Datas);

    if Length(Datas) = 0 then
      Exit;

    rc := root.AddChild('RootCollisionNode');
    rc.EditValues['Name'] := 'RCN';
    rc.NativeValues['Flags'] := 3;
    for var d in Datas do begin
      trishape := rc.AddChild('NiTriShape');
      trishape.NativeValues['Flags'] := 2;
      trishape.SetTransform(d.Transform);
      trishapedata := nif.AddBlock('NiTriShapeData');
      trishape.NativeValues['Data'] := trishapedata.Index;
      trishapedata.Assign(d.Data);
      trishapedata.NativeValues['Num UV Sets'] := 0;
      trishapedata.Elements['UV Sets'].Count := 0;
      trishapedata.NativeValues['Num Match Groups'] := 0;
      trishapedata.Elements['Match Groups'].Count := 0;
      trishapedata.NativeValues['Has UV'] := 0;
      trishapedata.NativeValues['Has Vertex Colors'] := 0;
      trishape.ApplyTransform;
    end;

    nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;


end.
