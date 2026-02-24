{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcCopyGeometryBlocks;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls, Vcl.ExtCtrls, Vcl.Mask;

type
  TFrameCopyGeometryBlocks = class(TFrame)
    StaticText1: TStaticText;
    edSourceDirectory: TLabeledEdit;
    btnBrowse: TButton;
    chkCopyGeom: TCheckBox;
    chkCopyTransform: TCheckBox;
    procedure btnBrowseClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcCopyGeometryBlocks = class(TProcBase)
  private
    Frame: TFrameCopyGeometryBlocks;
    fSourceDirectory: string;
    fCopyTransform: Boolean;
    fCopyGeom: Boolean;
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

procedure TFrameCopyGeometryBlocks.btnBrowseClick(Sender: TObject);
var
  path: string;
begin
  path := edSourceDirectory.Text;

  if path = '' then
    path := ExtractFilePath(Application.ExeName);

  if SelectFolder(path) then
    edSourceDirectory.Text := Path;
end;

constructor TProcCopyGeometryBlocks.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Copy geometry blocks';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif'];
end;

function TProcCopyGeometryBlocks.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameCopyGeometryBlocks.Create(aOwner);
  Result := Frame;
end;

procedure TProcCopyGeometryBlocks.OnShow;
begin
  Frame.edSourceDirectory.Text := StorageGetString('sSourceDirectory', Frame.edSourceDirectory.Text);
  Frame.chkCopyGeom.Checked := StorageGetBool('bCopyGeom', Frame.chkCopyGeom.Checked);
  Frame.chkCopyTransform.Checked := StorageGetBool('bCopyTransform', Frame.chkCopyTransform.Checked);
end;

procedure TProcCopyGeometryBlocks.OnHide;
begin
  StorageSetString('sSourceDirectory', Frame.edSourceDirectory.Text);
  StorageSetBool('bCopyGeom', Frame.chkCopyGeom.Checked);
  StorageSetBool('bCopyTransform', Frame.chkCopyTransform.Checked);
end;

procedure TProcCopyGeometryBlocks.OnStart;
begin
  fCopyGeom := Frame.chkCopyGeom.Checked;
  fCopyTransform := Frame.chkCopyTransform.Checked;

  if not fCopyGeom and not fCopyTransform then
    raise Exception.Create('Nothing to copy');

  fSourceDirectory := Frame.edSourceDirectory.Text;

  if (fSourceDirectory = '') or not DirectoryExists(fSourceDirectory) then
    raise Exception.Create('Source directory not found');

  fSourceDirectory := IncludeTrailingPathDelimiter(fSourceDirectory);
end;

function TProcCopyGeometryBlocks.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  Nif, SrcNif: TwbNifFile;
  j: Integer;
  Block, SrcBlock: TwbNifBlock;
  bChanged: Boolean;
  links, extras: array of Integer;
begin
  if not FileExists(fSourceDirectory + aFileName) then
    Exit;

  Nif := TwbNifFile.Create;
  SrcNif := TwbNifFile.Create;
  bChanged := False;

  try
    Nif.LoadFromFile(aInputDirectory + aFileName);
    SrcNif.LoadFromFile(fSourceDirectory + aFileName);

    for Block in Nif.BlocksByType('NiAVObject', True) do begin
      var name := Block.EditValues['Name'];
      if name = '' then
        Continue;

      // find the same block to copy from
      SrcBlock := SrcNif.BlockByName(name);
      if not Assigned(SrcBlock) then
        Continue;

      if Block.BlockType <> SrcBlock.BlockType then
        Continue;

      // copy Transform
      if fCopyTransform then begin
        Block.Elements['Transform'].Assign(SrcBlock.Elements['Transform']);
        // always force copy
        bChanged := True;
      end;

      // copy geometry
      if fCopyGeom then
      if Block.IsNiObject('NiTriBasedGeom') then begin
        // get the Data
        var SrcData := TwbNifBlock(SrcBlock.Elements['Data'].LinksTo);
        var DstData := TwbNifBlock(Block.Elements['Data'].LinksTo);

        if not Assigned(SrcData) or not Assigned(DstData) then
          Continue;

        SetLength(links, 1);
        links[0] := DstData.NativeValues['Additional Data'];
        DstData.Assign(SrcData);
        DstData.NativeValues['Additional Data'] := links[0];

        // copy Oblivion tangents in extra data block
        if Nif.NifVersion = nfTES4 then begin
          var SrcTan := SrcBlock.ExtraDataByName(sTES4TangentsExtraDataName);
          var DstTan := Block.ExtraDataByName(sTES4TangentsExtraDataName);
          if Assigned(SrcTan) then begin
            if not Assigned(DstTan) then begin
              DstTan := Block.AddExtraData('NiBinaryExtraData');
              DstTan.EditValues['Name'] := sTES4TangentsExtraDataName;
            end;
            DstTan.Assign(SrcTan);
          end

          else if Assigned(DstTan) then begin
            DstTan.RemoveBranch(True);
            Nif.Options := [nfoCollapseLinkArrays];
          end;
        end;

        bChanged := True;
      end

      else if Block.IsNiObject('BSTriShape') then begin
        SetLength(links, 5);
        links[0] := Block.NativeValues['Controller'];
        links[1] := Block.NativeValues['Collision Object'];
        links[2] := Block.NativeValues['Skin'];
        links[3] := Block.NativeValues['Shader Property'];
        links[4] := Block.NativeValues['Alpha Property'];
        SetLength(extras, Block.Elements['Extra Data List'].Count);
        for j := Low(extras) to High(extras) do
          extras[j] := Block.Elements['Extra Data List'][j].NativeValue;

        Block.Assign(SrcBlock);

        Block.NativeValues['Controller'] := links[0];
        Block.NativeValues['Collision Object'] := links[1];
        Block.NativeValues['Skin'] := links[2];
        Block.NativeValues['Shader Property'] := links[3];
        Block.NativeValues['Alpha Property'] := links[4];
        Block.Elements['Extra Data List'].Count := Length(extras);
        for j := Low(extras) to High(extras) do
          Block.Elements['Extra Data List'][j].NativeValue := extras[j];

        bChanged := True;
      end;

    end;

    if bChanged then
      Nif.SaveToData(Result);

  finally
    Nif.Free;
    SrcNif.Free;
  end;

end;


end.
