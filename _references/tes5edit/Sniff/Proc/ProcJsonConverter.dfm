object FrameJsonConverter: TFrameJsonConverter
  Left = 0
  Top = 0
  Width = 473
  Height = 308
  TabOrder = 0
  DesignSize = (
    473
    308)
  object Label1: TLabel
    Left = 16
    Top = 123
    Width = 137
    Height = 15
    Caption = 'Transform rotation format'
  end
  object Label2: TLabel
    Left = 16
    Top = 147
    Width = 441
    Height = 62
    Anchors = [akLeft, akTop, akRight]
    AutoSize = False
    Caption = 
      'AngleAxis is recommended if intention is to convert to JSON and ' +
      'back. Euler is more readable for text comparison but might cause' +
      ' data consistency issues in some cases when converting back to N' +
      'IF. This setting MUST MATCH between conversions!'
    WordWrap = True
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 473
    Height = 33
    Align = alTop
    AutoSize = False
    Caption = 
      'Convert files to JSON format and back. Can be used for viewing, ' +
      'comparing in text diff tools or manual tweaking in text editors ' +
      'or other software.'
    TabOrder = 0
  end
  object rbToJson: TRadioButton
    Left = 16
    Top = 48
    Width = 177
    Height = 17
    Caption = 'Convert NIF/KF to JSON'
    Checked = True
    TabOrder = 1
    TabStop = True
    WordWrap = True
    OnClick = rbToJsonClick
  end
  object rbFromJson: TRadioButton
    Left = 215
    Top = 47
    Width = 193
    Height = 19
    Caption = 'Convert JSON to NIF/KF'
    TabOrder = 2
    WordWrap = True
    OnClick = rbToJsonClick
  end
  object edExtension: TLabeledEdit
    Left = 231
    Top = 72
    Width = 33
    Height = 23
    EditLabel.Width = 203
    EditLabel.Height = 23
    EditLabel.Caption = 'extension for converted files if missing'
    LabelPosition = lpRight
    LabelSpacing = 4
    TabOrder = 3
    Text = 'nif'
  end
  object edDigits: TLabeledEdit
    Left = 32
    Top = 71
    Width = 33
    Height = 23
    EditLabel.Width = 124
    EditLabel.Height = 23
    EditLabel.Caption = 'decimal digits for floats'
    LabelPosition = lpRight
    LabelSpacing = 4
    TabOrder = 4
    Text = ''
  end
  object cmbRotation: TComboBox
    Left = 184
    Top = 118
    Width = 145
    Height = 23
    Style = csDropDownList
    ItemIndex = 0
    TabOrder = 5
    Text = 'AngleAxis AXYZ'
    Items.Strings = (
      'AngleAxis AXYZ'
      'Euler YPR')
  end
end
