{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcMergeShapes;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls, Vcl.Mask, Vcl.ExtCtrls;

type
  TFrameMergeShapes = class(TFrame)
    StaticText1: TStaticText;
    edNames: TLabeledEdit;
    chkExactMatch: TCheckBox;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcMergeShapes = class(TProcBase)
  private
    Frame: TFrameMergeShapes;
    fNames: array of string;
    fExactMatch: Boolean;
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
  StrUtils,
  wbDataFormat,
  wbDataFormatNif;

constructor TProcMergeShapes.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Merge shapes';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcMergeShapes.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameMergeShapes.Create(aOwner);
  Result := Frame;
end;

procedure TProcMergeShapes.OnShow;
begin
  Frame.edNames.Text := StorageGetString('sNames', Frame.edNames.Text);
  Frame.chkExactMatch.Checked := StorageGetBool('bExactMatch', Frame.chkExactMatch.Checked);
end;

procedure TProcMergeShapes.OnHide;
begin
  StorageSetString('sNames', Frame.edNames.Text);
  StorageSetBool('bExactMatch', Frame.chkExactMatch.Checked);
end;

procedure TProcMergeShapes.OnStart;
begin
  with TStringList.Create do try
    Delimiter := ',';
    StrictDelimiter := True;
    DelimitedText := Frame.edNames.Text;
    SetLength(fNames, Count);
    for var i: Integer := 0 to Pred(Count) do
      fNames[i] := Trim(Strings[i]);
  finally
    Free;
  end;

  if Length(fNames) = 0 then
    raise Exception.Create('Names field can not be empty');

  fExactMatch := Frame.chkExactMatch.Checked;
end;

function TProcMergeShapes.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;

  function merge_arrays(dst, src: TdfElement): Boolean;
  var
    i, v: Integer;
  begin
    Result := True;
    // both arrays are missing, nothing to do
    if not Assigned(src) and not Assigned(dst) then
      Exit;

    // if either is missing, error
    Result := Assigned(src) and Assigned(dst);
    if not Result then
      Exit;

    v := dst.Count;
    dst.Count := v + src.Count;
    for i := 0 to Pred(src.Count) do
      dst[v + i].Assign(src[i]);
  end;

  function merge_tris(dst, src: TdfElement; v: Integer): Boolean;
  var
    i, t: Integer;
  begin
    Result := Assigned(src) and Assigned(dst);
    if not Result then
      Exit;

    t := dst.Count;
    dst.Count := t + src.Count;
    for i := 0 to Pred(src.Count) do begin
      dst[t + i].NativeValues['[0]'] := src[i].NativeValues['[0]'] + v;
      dst[t + i].NativeValues['[1]'] := src[i].NativeValues['[1]'] + v;
      dst[t + i].NativeValues['[2]'] := src[i].NativeValues['[2]'] + v;
    end;
  end;

  function GetVerts(aShape: TwbNifBlock): Integer;
  begin
    Result := 0;
    if aShape.IsNiObject('BSTriShape') then
      Result := aShape.NativeValues['Num Vertices']
    else begin
      var data := aShape.Elements['Data'].LinksTo;
      if Assigned(data) then
        Result := data.NativeValues['Num Vertices'];
    end;
  end;

  function GetTris(aShape: TwbNifBlock): Integer;
  begin
    Result := 0;
    if aShape.IsNiObject('BSTriShape') then
      Result := aShape.NativeValues['Num Triangles']
    else begin
      var data := aShape.Elements['Data'].LinksTo;
      if Assigned(data) then
        Result := data.NativeValues['Num Triangles'];
    end;
  end;

var
  nif: TwbNifFile;
  n, shape, merged, toremove: TwbNifBlock;
  children, shape_uv, merged_uv: TdfElement;
  i, j, v, verts, tris: Integer;
  bChanged, bMatched: Boolean;
begin
  bChanged := False;
  nif := TwbNifFile.Create;
  nif.Options := [nfoCollapseLinkArrays, nfoRemoveUnusedStrings];

  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    var nodes := nif.BlocksByType('NiNode', True);

    for n in nodes do begin

      bMatched := False;
      var name: String := n.EditValues['Name'];
      for var s: String in fNames do
        if ( fExactMatch and SameText(name, s) ) or ( not fExactMatch and ContainsText(name, s) ) then
          bMatched := True;

      if not bMatched then
        Continue;

      children := n.Elements['Children'];
      if not Assigned(children) then
        Exit;

      // check for vertices and tris overflow before merging
      verts := 0; tris := 0;
      for i := 0 to Pred(Children.Count) do begin
        shape := TwbNifBlock(children[i].LinksTo);
        if Assigned(shape) then begin
          Inc(verts, GetVerts(shape));
          Inc(tris, GetTris(shape));
        end;
      end;

      // just skip such nodes
      if (verts > High(Word)) or (tris > High(Word)) then
        Continue;

      merged := nil;

      // merging BSTriShape
      if nif.NifVersion in [nfSSE, nfFO4] then
        for i := 0 to Pred(children.Count) do begin
          shape := TwbNifBlock(children[i].LinksTo);
          if not Assigned(shape) or (shape.BlockType <> 'BSTriShape') then
            Continue;

          shape.ApplyTransform;

          if not Assigned(merged) then begin
            merged := shape;
            Continue;
          end;

          v := merged.Elements['Vertex Data'].Count;
          // can't merge if vertices overflow
          //if v + shape.Elements['Vertex Data'].Count > High(Word) then
          //  Exit;

          merge_arrays(merged.Elements['Vertex Data'], shape.Elements['Vertex Data']);
          merge_tris(merged.Elements['Triangles'], shape.Elements['Triangles'], v);

          merged.NativeValues['Num Vertices'] := merged.Elements['Vertex Data'].Count;
          merged.NativeValues['Num Triangles'] := merged.Elements['Triangles'].Count;

          children[i].NativeValue := -1;
          shape.RemoveBranch;
          bChanged := True;
        end;


      // merging NiTriShape and NiTriStrips
      if nif.NifVersion in [nfTES3, nfTES4, nfFO3, nfTES5] then
        for i := 0 to Pred(children.Count) do begin
          shape := TwbNifBlock(children[i].LinksTo);
          if not Assigned(shape) or not shape.IsNiObject('NiTriBasedGeom') then
            Continue;

          // triangulate if strips
          if shape.BlockType = 'NiTriStrips' then begin
            j := shape.Index;
            nif.ConvertBlock(j, 'NiTriShape');
            shape := nif.Blocks[j];
          end;

          shape.ApplyTransform;

          toremove := shape; // store shape block to remove later
          shape := TwbNifBlock(shape.Elements['Data'].LinksTo);
          if not Assigned(shape) then
            Continue;

          // triangulate if strips data
          if shape.BlockType = 'NiTriStripsData' then begin
            j := shape.Index;
            nif.ConvertBlock(j, 'NiTriShapeData');
            shape := nif.Blocks[j];
          end;

          if not Assigned(merged) then begin
            merged := shape;
            Continue;
          end;

          v := merged.Elements['Vertices'].Count;
          // can't merge if vertices overflow
          //if v + shape.Elements['Vertices'].Count > High(Word) then
          //  Exit;

          merge_arrays(merged.Elements['Vertices'], shape.Elements['Vertices']);
          merge_arrays(merged.Elements['Normals'], shape.Elements['Normals']);
          merge_arrays(merged.Elements['Tangents'], shape.Elements['Tangents']);
          merge_arrays(merged.Elements['Bitangents'], shape.Elements['Bitangents']);
          merge_arrays(merged.Elements['Vertex Colors'], shape.Elements['Vertex Colors']);

          merged_uv := merged.Elements['UV Sets'];
          shape_uv := shape.Elements['UV Sets'];
          if Assigned(merged_uv) and Assigned(shape_uv) and (merged_uv.Count > 0) and (shape_uv.Count > 0) then
            merge_arrays(merged_uv[0], shape_uv[0]);

          merge_tris(merged.Elements['Triangles'], shape.Elements['Triangles'], v);
          merged.NativeValues['Num Vertices'] := merged.Elements['Vertices'].Count;
          merged.NativeValues['Num Triangles'] := merged.Elements['Triangles'].Count;

          children[i].NativeValue := -1;
          toremove.RemoveBranch;
          bChanged := True;
        end;

      if Assigned(merged) then begin
        merged.UpdateBounds;
        if nif.NifVersion = nfTES4 then
          merged.UpdateTangents(False);
      end;
    end;

    if bChanged then
      nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;

end.
