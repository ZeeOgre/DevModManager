{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcAddBoundingBox;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls, Vcl.ExtCtrls, Vcl.Mask;

type
  TFrameAddBoundingBox = class(TFrame)
    Label1: TLabel;
    Label2: TLabel;
    edCenterX: TLabeledEdit;
    edCenterY: TLabeledEdit;
    edCenterZ: TLabeledEdit;
    edExtentX: TLabeledEdit;
    edExtentY: TLabeledEdit;
    edExtentZ: TLabeledEdit;
    StaticText1: TStaticText;
    Label3: TLabel;
    edFlags: TEdit;
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcAddBoundingBox = class(TProcBase)
  private
    Frame: TFrameAddBoundingBox;
    fFlags: Integer;
    fCenterX, fCenterY, fCenterZ: string;
    fExtentX, fExtentY, fExtentZ: string;
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

constructor TProcAddBoundingBox.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Add bounding box';
  fSupportedGames := [gtTES3];
  fExtensions := ['nif'];
end;

function TProcAddBoundingBox.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameAddBoundingBox.Create(aOwner);
  Result := Frame;
end;

procedure TProcAddBoundingBox.OnShow;
begin
  Frame.edFlags.Text := StorageGetString('sFlags', Frame.edFlags.Text);
  Frame.edCenterX.Text := StorageGetString('sCenterX', Frame.edCenterX.Text);
  Frame.edCenterY.Text := StorageGetString('sCenterY', Frame.edCenterY.Text);
  Frame.edCenterZ.Text := StorageGetString('sCenterZ', Frame.edCenterZ.Text);
  Frame.edExtentX.Text := StorageGetString('sExtentX', Frame.edExtentX.Text);
  Frame.edExtentY.Text := StorageGetString('sExtentY', Frame.edExtentY.Text);
  Frame.edExtentZ.Text := StorageGetString('sExtentZ', Frame.edExtentZ.Text);
end;

procedure TProcAddBoundingBox.OnHide;
begin
  StorageSetString('sFlags', Frame.edFlags.Text);
  StorageSetString('sCenterX', Frame.edCenterX.Text);
  StorageSetString('sCenterY', Frame.edCenterY.Text);
  StorageSetString('sCenterZ', Frame.edCenterZ.Text);
  StorageSetString('sExtentX', Frame.edExtentX.Text);
  StorageSetString('sExtentY', Frame.edExtentY.Text);
  StorageSetString('sExtentZ', Frame.edExtentZ.Text);
end;

procedure TProcAddBoundingBox.OnStart;

  function GetVerifyFloat(const s: string): string;
  begin
    Result := Trim(s);
    if Result <> '' then
      dfStrToFloat(Result)
    else
      Result := '0';
  end;

begin
  fFlags := StrToIntDef(Frame.edFlags.Text, 0);
  fCenterX := GetVerifyFloat(Frame.edCenterX.Text);
  fCenterY := GetVerifyFloat(Frame.edCenterY.Text);
  fCenterZ := GetVerifyFloat(Frame.edCenterZ.Text);
  fExtentX := GetVerifyFloat(Frame.edExtentX.Text);
  fExtentY := GetVerifyFloat(Frame.edExtentY.Text);
  fExtentZ := GetVerifyFloat(Frame.edExtentZ.Text);
end;

function TProcAddBoundingBox.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
const
  sBoundingBox = 'Bounding Box';
var
  nif: TwbNifFile;
  root, bv: TwbNifBlock;
begin
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    if nif.BlocksCount = 0 then
      Exit;

    root := nif.RootNode;

    var children := root.Elements['Children'];
    if not Assigned(children) then
      Exit;


    for var i: Integer := 0 to Pred(children.Count) do begin
      var child := children[i].LinksTo;
      if Assigned(child) and (child.EditValues['Name'] = sBoundingBox) then
        Exit;
    end;

    bv := root.AddChild('NiNode');
    bv.EditValues['Name'] := sBoundingBox;
    bv.NativeValues['Flags'] := fFlags;
    bv.EditValues['Has Bounding Volume'] := 'yes';
    bv.EditValues['Bounding Volume\Collision Type'] := 'BOX_BV';
    var v := bv.Elements['Bounding Volume\Box'];
    v.EditValues['Center\X'] := fCenterX;
    v.EditValues['Center\Y'] := fCenterY;
    v.EditValues['Center\Z'] := fCenterZ;
    v.EditValues['Extent\X'] := fExtentX;
    v.EditValues['Extent\Y'] := fExtentY;
    v.EditValues['Extent\Z'] := fExtentZ;
    v.EditValues['Axis\[0]\X'] := '1.0';
    v.EditValues['Axis\[1]\Y'] := '1.0';
    v.EditValues['Axis\[2]\Z'] := '1.0';

    nif.SaveToData(Result);

  finally
    nif.Free;
  end;

end;

end.
