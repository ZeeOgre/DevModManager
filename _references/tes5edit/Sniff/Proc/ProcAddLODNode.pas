{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcAddLODNode;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, Vcl.StdCtrls, SniffProcessor;

type
  TFrameAddLODNode = class(TFrame)
    StaticText1: TStaticText;
    memoExtents: TMemo;
    Label1: TLabel;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcAddLODNode = class(TProcBase)
  private
    Frame: TFrameAddLODNode;
    fExtents: TArray<Double>;
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
  System.StrUtils,
  wbDataFormat,
  wbDataFormatNif;

constructor TProcAddLODNode.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Add NiLODNode';
  fSupportedGames := [gtTES4, gtFO3, gtFNV];
  fExtensions := ['nif'];
end;

function TProcAddLODNode.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameAddLODNode.Create(aOwner);
  Result := Frame;
end;

procedure TProcAddLODNode.OnShow;
begin
  Frame.memoExtents.Text := StringToText(StorageGetString('sExtents', Frame.memoExtents.Text));
end;

procedure TProcAddLODNode.OnHide;
begin
  StorageSetString('sExtents', TextToString(Frame.memoExtents.Text));
end;

procedure TProcAddLODNode.OnStart;
var
  extent: Double;
begin
  fExtents := [0];
  for var i := 0 to Pred(Frame.memoExtents.Lines.Count) do begin
    var s := Trim(Frame.memoExtents.Lines[i]);
    if s = '' then
      Continue;
    try
      extent := dfStrToFloat(s);
      fExtents := fExtents + [extent];
    except
      raise Exception.CreateFmt('Line %d has invalid extent value', [i+1]);
    end;
  end;
end;

function TProcAddLODNode.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
  root, child, LODNode, RangeNode: TwbNifBlock;
  entries, entry, level: TdfElement;
  shapes: TList;
begin
  shapes := TList.Create;
  nif := TwbNifFile.Create;
  nif.Options := [nfoCollapseLinkArrays];
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.BlocksCount = 0 then
      Exit;

    root := nif.RootNode;
    if root.BlockType <> 'BSFadeNode' then
      Exit;

    entries := root.Elements['Children'];
    if not Assigned(entries)then
      Exit;

    LODNode := nil;
    for var i := 0 to Pred(entries.Count) do begin
      entry := entries[i];
      child := TwbNifBlock(entry.LinksTo);
      if not Assigned(child) then
        Continue;
      // checking for existing NiLODNode block
      if child.BlockType = 'NiLODNode' then
        LODNode := child
      // collecting strips/shapes
      else if child.IsNiObject('NiTriBasedGeom') then
        shapes.Add(entry);
    end;

    if shapes.Count < 2 then
      Exit;

    if not Assigned(LODNode) then begin
      // inserting NiLODNode at the position of the first shape
      LODNode := nif.InsertBlock(TdfElement(shapes[0]).NativeValue, 'NiLODNode');
      // adding to the root's children
      entries.Add.NativeValue := LODNode.Index;
      RangeNode := nif.AddBlock('NiRangeLODData');
      LODNode.NativeValues['LOD Level Data'] := RangeNode.Index;
    end
    else begin
      RangeNode := nif.BlockByType('NiRangeLODData');
      if not Assigned(RangeNode) then
        Exit;
    end;

    // moving shapes under NiLODNode
    for var i := 0 to Pred(shapes.Count) do begin
      LODNode.Elements['Children'].Add.NativeValue := TdfElement(shapes[i]).NativeValue;
      TdfElement(shapes[i]).NativeValue := -1;
      // adding LOD Level for each moved shape
      level := RangeNode.Elements['LOD Levels'].Add;
      if i + 1 <= High(fExtents) then begin
        level.NativeValues['Near Extent'] := fExtents[i];
        level.NativeValues['Far Extent'] := fExtents[i + 1];
      end;
    end;

    nif.SaveToData(Result);

  finally
    nif.Free;
    shapes.Free;
  end;

end;


end.
