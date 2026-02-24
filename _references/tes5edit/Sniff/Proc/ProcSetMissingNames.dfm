object FrameSetMissingNames: TFrameSetMissingNames
  Left = 0
  Top = 0
  Width = 528
  Height = 327
  TabOrder = 0
  object Label1: TLabel
    Left = 32
    Top = 54
    Width = 399
    Height = 15
    Caption = 
      'Quite often it'#39's got meaningless name like "Scene root" making l' +
      'ogs useless'
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 528
    Height = 25
    Align = alTop
    AutoSize = False
    Caption = 
      'Rename empty NiNode names after the file name to have more infor' +
      'mation in crash logs'
    TabOrder = 0
  end
  object chkRenameRoot: TCheckBox
    Left = 16
    Top = 31
    Width = 241
    Height = 17
    Caption = 'Always rename the root node'
    Checked = True
    State = cbChecked
    TabOrder = 1
  end
end
