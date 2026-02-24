object FrameHavokMaterial: TFrameHavokMaterial
  Left = 0
  Top = 0
  Width = 482
  Height = 330
  TabOrder = 0
  object Label1: TLabel
    Left = 16
    Top = 48
    Width = 91
    Height = 13
    Caption = 'Search for material'
  end
  object Label2: TLabel
    Left = 16
    Top = 101
    Width = 102
    Height = 13
    Caption = 'Replace with material'
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 482
    Height = 33
    Align = alTop
    AutoSize = False
    Caption = 
      'Search for Havok material in bhkShape* nodes. Leave "Replace wit' +
      'h" field empty to only report found files.'
    TabOrder = 0
  end
  object cmbSearch: TComboBox
    Left = 16
    Top = 67
    Width = 377
    Height = 21
    Style = csDropDownList
    DropDownCount = 24
    TabOrder = 1
  end
  object cmbReplace: TComboBox
    Left = 16
    Top = 120
    Width = 377
    Height = 21
    Style = csDropDownList
    DropDownCount = 24
    TabOrder = 2
  end
end
