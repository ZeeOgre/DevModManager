object FrameConvertRootNode: TFrameConvertRootNode
  Left = 0
  Top = 0
  Width = 477
  Height = 293
  TabOrder = 0
  object Label1: TLabel
    Left = 16
    Top = 37
    Width = 28
    Height = 15
    Caption = 'From'
  end
  object Label2: TLabel
    Left = 248
    Top = 37
    Width = 12
    Height = 15
    Caption = 'To'
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 477
    Height = 25
    Align = alTop
    AutoSize = False
    Caption = 
      'Convert blocks type including necessary updates in the new block' +
      's.'
    TabOrder = 0
    ExplicitWidth = 435
  end
  object cmbNodeFrom: TComboBox
    Left = 16
    Top = 56
    Width = 217
    Height = 23
    Style = csDropDownList
    TabOrder = 1
    OnSelect = cmbNodeFromSelect
  end
  object cmbNodeTo: TComboBox
    Left = 248
    Top = 56
    Width = 217
    Height = 23
    Style = csDropDownList
    TabOrder = 2
  end
  object chkRoot: TCheckBox
    Left = 16
    Top = 85
    Width = 145
    Height = 17
    Caption = 'Root node only'
    TabOrder = 3
  end
end
