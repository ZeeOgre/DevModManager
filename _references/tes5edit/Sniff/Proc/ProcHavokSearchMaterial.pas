unit ProcHavokSearchMaterial;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls;

type
  TFrameHavokMaterial = class(TFrame)
    StaticText1: TStaticText;
    cmbSearch: TComboBox;
    cmbReplace: TComboBox;
    Label1: TLabel;
    Label2: TLabel;
  private
    { Private declarations }
  public
    { Public declarations }
    Proc: TProcBase;
  end;

  TProcHavokSearchMaterial = class(TProcBase)
  private
    Frame: TFrameHavokMaterial;
    fMaterialSearch: string;
    fMaterialReplace: string;
  public
    slMaterial: TStringList;

    constructor Create(aManager: TProcManager); override;
    destructor Destroy; override;
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
  wbDataFormatNifTypes;

constructor TProcHavokSearchMaterial.Create(aManager: TProcManager);
var
  i: Integer;
begin
  inherited;

  fTitle := 'Search for Havok material';
  fSupportedGames := [gtTES4, gtFO3, gtFNV, gtTES5, gtSSE];
  fExtensions := ['nif'];

  slMaterial := TStringList.Create;
  with TdfEnumDef(wbOblivionHavokMaterial('', '', [])) do try
    for i := 0 to Pred(ValuesMapCount) do
      slMaterial.Add(Values[i]);
  finally
    Free;
  end;
  with TdfEnumDef(wbFallout3HavokMaterial('', '', [])) do try
    for i := 0 to Pred(ValuesMapCount) do
      slMaterial.Add(Values[i]);
  finally
    Free;
  end;
  with TdfEnumDef(wbSkyrimHavokMaterial('', '', [])) do try
    for i := 0 to Pred(ValuesMapCount) do
      slMaterial.Add(Values[i]);
  finally
    Free;
  end;
  slMaterial.Sort;
  slMaterial.Insert(0, '');
end;

destructor TProcHavokSearchMaterial.Destroy;
begin
  slMaterial.Free;
end;

function TProcHavokSearchMaterial.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameHavokMaterial.Create(aOwner);
  Frame.Proc := Self;
  Result := Frame;
end;

procedure TProcHavokSearchMaterial.OnShow;
var
  i: Integer;
begin
  Frame.cmbSearch.Items.Assign(slMaterial);
  i := Frame.cmbSearch.Items.IndexOf(StorageGetString('sMaterialSearch', ''));
  if i = -1 then i := 0;
  Frame.cmbSearch.ItemIndex := i;

  Frame.cmbReplace.Items.Assign(slMaterial);
  i := Frame.cmbReplace.Items.IndexOf(StorageGetString('sMaterialReplace', ''));
  if i = -1 then i := 0;
  Frame.cmbReplace.ItemIndex := i;
end;

procedure TProcHavokSearchMaterial.OnHide;
begin
  StorageSetString('sMaterialSearch', Frame.cmbSearch.Text);
  StorageSetString('sMaterialReplace', Frame.cmbReplace.Text);
end;

procedure TProcHavokSearchMaterial.OnStart;
begin
  fMaterialSearch := Frame.cmbSearch.Text;
  fMaterialReplace := Frame.cmbReplace.Text;

  if fMaterialSearch = '' then
    raise Exception.Create('Searched material can not be empty');

  if fMaterialSearch = fMaterialReplace then
    raise Exception.Create('Searched and replacing materials must be different');
end;

function TProcHavokSearchMaterial.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;

  procedure UpdateField(const el: TdfElement; const aValue: string; var aChanged: Boolean);
  begin
    if not Assigned(el) or (aValue = '') then
      Exit;

    if el.EditValue <> aValue then begin
      el.EditValue := aValue;
      aChanged := True;
    end;
  end;

var
  nif: TwbNifFile;
  i: Integer;
  block: TwbNifBlock;
  bChanged: Boolean;
  Log: TStringList;
begin
  bChanged := False;
  Log := TStringList.Create;
  nif := TwbNifFile.Create;
  try
    nif.LoadFromFile(aInputDirectory + aFileName);

    for i := 0 to Pred(nif.BlocksCount) do begin
      block := nif.Blocks[i];

      if (block.BlockType = 'hkPackedNiTriStripsData') or (block.BlockType = 'bhkCompressedMeshShapeData') then begin
        var subshapes := block.Elements['Sub Shapes'];
        if not Assigned(subshapes) then
          subshapes := block.Elements['Chunk Materials'];
        if not Assigned(subshapes) then
          Continue;

        for var j := 0 to Pred(subshapes.Count) do begin
          var subshape := subshapes[j];
          if subshape.EditValues['Material'] <> fMaterialSearch then
            Continue;
          if fMaterialReplace = '' then
            Log.Add(Format(#9 + block.Name + ': Shape #%d uses %s material', [j, fMaterialSearch]))
          else
            UpdateField(subshape.Elements['Material'], fMaterialReplace, bChanged);
        end;
      end

      else if block.IsNiObject('bhkShape', True) then begin
        if block.EditValues['Material'] <> fMaterialSearch then
          Continue;

        if fMaterialReplace = '' then
          Log.Add(Format(#9 + block.Name + ': Uses %s material', [fMaterialSearch]))
        else
          UpdateField(block.Elements['Material'], fMaterialReplace, bChanged);
      end;

    end;

    if Log.Count > 0 then begin
      fManager.AddMessage(aFileName);
      Log.Add('');
      fManager.AddMessages(Log);
    end;

    if bChanged then
      nif.SaveToData(Result);

  finally
    Log.Free;
    nif.Free;
  end;

end;

end.
