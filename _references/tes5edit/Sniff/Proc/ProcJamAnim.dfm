object FrameJamAnim: TFrameJamAnim
  Left = 0
  Top = 0
  Width = 490
  Height = 272
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 490
    Height = 19
    Align = alTop
    Caption = 
      'Add NiTransformData to NiTransformInterpolator with Quaternion R' +
      'otation keys and Translations'
    TabOrder = 0
  end
  object chkAddRotation: TCheckBox
    Left = 16
    Top = 47
    Width = 145
    Height = 17
    Caption = 'Add Rotation keys'
    Checked = True
    State = cbChecked
    TabOrder = 1
  end
  object chkAddTranslation: TCheckBox
    Left = 16
    Top = 78
    Width = 145
    Height = 17
    Caption = 'Add Translation keys'
    Checked = True
    State = cbChecked
    TabOrder = 2
  end
end
