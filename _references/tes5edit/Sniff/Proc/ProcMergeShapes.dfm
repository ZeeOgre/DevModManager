object FrameMergeShapes: TFrameMergeShapes
  Left = 0
  Top = 0
  Width = 482
  Height = 257
  TabOrder = 0
  DesignSize = (
    482
    257)
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 482
    Height = 41
    Align = alTop
    AutoSize = False
    Caption = 
      'Merge BSTriShape, NiTriShape and NiTriStrips blocks under NiNode' +
      's matched by name. All children shapes will be merged into the f' +
      'irst one and removed if not used anymore.'
    TabOrder = 0
  end
  object edNames: TLabeledEdit
    Left = 16
    Top = 72
    Width = 369
    Height = 23
    Anchors = [akLeft, akTop, akRight]
    EditLabel.Width = 203
    EditLabel.Height = 15
    EditLabel.Caption = 'Names, separated by comma if several'
    TabOrder = 1
    Text = '.dds'
  end
  object chkExactMatch: TCheckBox
    Left = 16
    Top = 99
    Width = 121
    Height = 17
    Hint = 
      'Should block names match names exactly or partially (contain the' +
      'm)'
    Caption = 'Exact match'
    ParentShowHint = False
    ShowHint = True
    TabOrder = 2
  end
end
