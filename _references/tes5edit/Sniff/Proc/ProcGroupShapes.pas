{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcGroupShapes;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

type
  TFrameGroupShapes = class(TFrame)
    StaticText1: TStaticText;
    chkSplit: TCheckBox;
    chkAllFeatures: TCheckBox;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcGroupShapes = class(TProcBase)
  private
    Frame: TFrameGroupShapes;
    fSplit: Boolean;
    fAllFeatures: Boolean;
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

constructor TProcGroupShapes.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Group shapes';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcGroupShapes.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameGroupShapes.Create(aOwner);
  Result := Frame;
end;

procedure TProcGroupShapes.OnShow;
begin
  Frame.chkSplit.Checked := StorageGetBool('bSplit', Frame.chkSplit.Checked);
  Frame.chkAllFeatures.Checked := StorageGetBool('bAllFeatures', Frame.chkAllFeatures.Checked);
end;

procedure TProcGroupShapes.OnHide;
begin
  StorageSetBool('bSplit', Frame.chkSplit.Checked);
  StorageSetBool('bAllFeatures', Frame.chkAllFeatures.Checked);
end;

procedure TProcGroupShapes.OnStart;
begin
  fSplit := Frame.chkSplit.Checked;
  fAllFeatures := Frame.chkAllFeatures.Checked;
end;

function TProcGroupShapes.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;

  function GetUsedTexture(aShape: TwbNifBlock): string;
  var
    el, props: TdfElement;
    shader, prop, texset: TwbNifBlock;
    i, j: Integer;
  begin
    Result :=  '';

    el := aShape.Elements['Shader Property'];
    if Assigned(el) then begin
      shader := TwbNifBlock(el.LinksTo);
      if not Assigned(shader) then
        Exit;

      // skip animated shapes
      //el := shader.Elements['Controller'];
      //if Assigned(el) and (el.LinksTo <> nil) then
      //  Exit;

      // group by material file in the Name field for FO4 meshes
      if (nif.NifVersion = nfFO4) and (shader.BlockType = 'BSLightingShaderProperty') and (shader.EditValues['Name'] <> '') then begin
        Result := wbNormalizeResourceName(shader.EditValues['Name'], resMaterial);
        Exit;
      end;

      el := shader.Elements['Texture Set'];
      if not Assigned(el) then
        Exit;

      texset := TwbNifBlock(shader.Elements['Texture Set'].LinksTo);
      if not Assigned(texset) then
        Exit;

      el := texset.Elements['Textures'];
      if not Assigned(el) then
        Exit;

      for j := 0 to Pred(el.Count) do begin
        if Result <> '' then Result := Result + ',';
        Result := Result + wbNormalizeResourceName(el[j].EditValue, resTexture);
        if not fAllFeatures then
          Break;
      end;
    end

    else begin
      props := aShape.Elements['Properties'];
      if not Assigned(props) then
        Exit;

      for i := 0 to Pred(props.Count) do begin
        prop := TwbNifBlock(props[i].LinksTo);

        if prop.BlockType = 'NiTexturingProperty' then begin
          el := prop.Elements['Base Texture\Source'];
          if not Assigned(el) then
            Exit;

          prop := TwbNifBlock(el.LinksTo);
          if not Assigned(prop) then
            Continue;

          Result := wbNormalizeResourceName(prop.EditValues['File Name'], resTexture);
          Exit;
        end

        else if prop.BlockType = 'BSShaderPPLightingProperty' then begin
          el := prop.Elements['Texture Set'];
          if not Assigned(el) then
            Continue;

          texset := TwbNifBlock(el.LinksTo);
          if not Assigned(texset) then
            Continue;

          el := texset.Elements['Textures'];
          if not Assigned(el) then
            Exit;

          for j := 0 to Pred(el.Count) do begin
            if Result <> '' then Result := Result + ',';
            Result := Result + wbNormalizeResourceName(el[j].EditValue, resTexture);
            if not fAllFeatures then
              Break;
          end;

          Exit;
        end;

      end;
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
  children: TdfElement;
  i, j, idx, verts, tris: Integer;
  node, child: TwbNifBlock;
  token: string;
  tokens: array of record
    token: string;
    shapes: array of TwbNifBlock;
  end;
  bChanged: Boolean;
begin
  bChanged := False;
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.BlocksCount = 0 then
      Exit;

    children := nif.RootNode.Elements['Children'];
    if not Assigned(children) then
      Exit;

    // iterate over children of root node
    for i := 0 to Pred(children.Count) do begin
      child := TwbNifBlock(children[i].LinksTo);
      if not Assigned(child) then
        Continue;

      if not (child.IsNiObject('NiTriBasedGeom') or child.IsNiObject('BSTriShape')) then
        Continue;

      token := GetUsedTexture(child);
      if token = '' then
        Continue;

      // check if we already have such token
      idx := -1;
      for j := Low(tokens) to High(tokens) do
        if tokens[j].token = token then begin
          idx := j;
          Break;
        end;

      // adding new token
      if idx = -1 then begin
        idx := Length(tokens);
        SetLength(tokens, idx + 1);
        tokens[idx].token := token;
      end;

      // adding shape under token
      with tokens[idx] do begin
        SetLength(shapes, Length(shapes) + 1);
        shapes[Pred(Length(shapes))] := child;
      end;
    end;

    // iterate over collected tokens
    for i := Low(tokens) to High(tokens) do with tokens[i] do begin
      if Length(shapes) < 2 then
        Continue;

      node := nil;
      verts := 0; tris := 0;

      // iterate over token shapes
      for j := Low(shapes) to High(shapes) do begin

        if fSplit then begin
          var v := GetVerts(shapes[j]);
          var t := GetTris(shapes[j]);
          // if NiNode exists already, check that the current shape can fit there
          // otherwise force create new NiNode
          if (verts + v > High(Word)) or (tris + t > High(Word)) then begin
            node := nil;
            Verts := v;
            Tris := t;
          end else begin
            Inc(verts, v);
            Inc(tris, t);
          end;
        end;

        // create NiNode at the index of the current shape if doesn't exist yet
        if not Assigned(node) then begin
          node := nif.InsertBlock(shapes[j].Index, 'NiNode');
          var diffuse := ExtractFileName(token.Split([','])[0]);
          if diffuse = '' then diffuse := 'nodiffuse.dds';
          //diffuse := ChangeFileExt(diffuse, '');
          node.EditValues['Name'] := nif.GetUniqueName(diffuse);
        end;

        node.Elements['Children'].Add.NativeValue := shapes[j].Index;

        // find the link to the current shape in root's children
        for idx := 0 to Pred(children.Count) do
          if TwbNifBlock(children[idx].LinksTo) = shapes[j] then begin
            // if it is the first shape of created NiNode then relink to it
            if node.Elements['Children'].Count = 1 then
              children[idx].NativeValue := node.Index
            // otherwise delete the link
            else
              children.Delete(idx);

            Break;
          end;

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
