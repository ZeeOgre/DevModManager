{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit ProcJsonConverter;

interface

uses
  Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants, System.Classes,
  Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs, SniffProcessor,
  Vcl.StdCtrls, Vcl.ExtCtrls, Vcl.Mask;

type
  TFrameJsonConverter = class(TFrame)
    StaticText1: TStaticText;
    rbToJson: TRadioButton;
    rbFromJson: TRadioButton;
    edExtension: TLabeledEdit;
    edDigits: TLabeledEdit;
    cmbRotation: TComboBox;
    Label1: TLabel;
    Label2: TLabel;
    procedure rbToJsonClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

  TProcJsonConverter = class(TProcBase)
  private
    Frame: TFrameJsonConverter;
    fToJson: Boolean;
    fExtension: string;
    fDigitsOld: integer;
    fRotationEulerOld: Boolean;
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
  wbDataFormatNifTypes;

procedure TFrameJsonConverter.rbToJsonClick(Sender: TObject);
begin
  edDigits.Enabled := rbToJson.Checked;
  edExtension.Enabled := rbFromJson.Checked;
end;

constructor TProcJsonConverter.Create(aManager: TProcManager);
begin
  inherited;

  fTitle := 'Convert to and from JSON';
  fSupportedGames := [gtTES3, gtTES4, gtFO3, gtFNV, gtTES5, gtSSE, gtFO4];
  fExtensions := ['nif', 'kf', 'json'];
end;

function TProcJsonConverter.GetFrame(aOwner: TComponent): TFrame;
begin
  Frame := TFrameJsonConverter.Create(aOwner);
  Result := Frame;
end;

procedure TProcJsonConverter.OnShow;
begin
  fDigitsOld := dfFloatDecimalDigits;
  fRotationEulerOld := wbRotationEuler;
  Frame.rbToJson.Checked := StorageGetBool('bToJson', Frame.rbToJson.Checked);
  Frame.rbFromJson.Checked := not Frame.rbToJson.Checked;
  Frame.edExtension.Text := StorageGetString('sExtension', Frame.edExtension.Text);
  Frame.edDigits.Text := StorageGetString('sDigits', IntToStr(fDigitsOld));
  Frame.cmbRotation.ItemIndex := StorageGetInteger('iRotation', Frame.cmbRotation.ItemIndex);
  Frame.rbToJsonClick(nil);
end;

procedure TProcJsonConverter.OnHide;
begin
  dfFloatDecimalDigits := fDigitsOld;
  wbRotationEuler := fRotationEulerOld;
  StorageSetBool('bToJson', Frame.rbToJson.Checked);
  StorageSetString('sExtension', Frame.edExtension.Text);
  StorageSetString('sDigits', Frame.edDigits.Text);
  StorageSetInteger('iRotation', Frame.cmbRotation.ItemIndex);
end;

procedure TProcJsonConverter.OnStart;
begin
  fToJson := Frame.rbToJson.Checked;
  fExtension := Frame.edExtension.Text;
  wbRotationEuler := Frame.cmbRotation.ItemIndex = 1;

  if fToJson then begin
    if Frame.edDigits.Text = '' then
      Frame.edDigits.Text := IntToStr(fDigitsOld);

    var Digits := StrToIntDef(Frame.edDigits.Text, 0);
    if (Digits < 6) or (Digits > 16) then
      raise Exception.Create('Decimal digits can vary from 6 to 16');

    dfFloatDecimalDigits := Digits;
  end;

  if not fToJson and (fExtension = '') then
    raise Exception.Create('Default extension can not be empty');
end;

function TProcJsonConverter.ProcessFile(const aInputDirectory, aOutputDirectory: string; var aFileName: string): TBytes;
var
  nif: TwbNifFile;
begin
  nif := TwbNifFile.Create;
  try

    if fToJson then begin
      if SameText(ExtractFileExt(aFileName), '.json') then
        Exit;

      nif.LoadFromFile(aInputDirectory + aFileName);

      var ss: TStringStream;
      ss := TStringStream.Create;
      try
        ss.WriteString(nif.ToJSON(False));
        SetLength(Result, ss.Size);
        ss.Position := 0;
        ss.Read(Result[0], Length(Result));
      finally
        ss.Free;
      end;

      aFileName := aFileName + '.json';
    end

    else begin
      if not SameText(ExtractFileExt(aFileName), '.json') then
        Exit;

      nif.LoadFromJSONFile(aInputDirectory + aFileName);
      nif.SaveToData(Result);

      aFileName := ChangeFileExt(aFileName, '');
      if ExtractFileExt(aFileName) = '' then
        aFileName := aFileName + '.' + fExtension;
    end;

  finally
    nif.Free;
  end;

end;

end.
