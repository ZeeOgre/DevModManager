object FrameAttachParent: TFrameAttachParent
  Left = 0
  Top = 0
  Width = 479
  Height = 301
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 479
    Height = 25
    Align = alTop
    AutoSize = False
    Caption = 
      'Find NiNode or its descendant blocks by name and attach parent N' +
      'iNode'
    TabOrder = 0
  end
  object edFindNodeName: TLabeledEdit
    Left = 16
    Top = 48
    Width = 225
    Height = 23
    EditLabel.Width = 112
    EditLabel.Height = 15
    EditLabel.Caption = 'NiNode name to find'
    TabOrder = 1
    Text = '##SightingNode'
  end
  object edAttachNodeName: TLabeledEdit
    Left = 16
    Top = 104
    Width = 225
    Height = 23
    EditLabel.Width = 162
    EditLabel.Height = 15
    EditLabel.Caption = 'Attached parent NiNode name'
    TabOrder = 2
    Text = '##ISControl'
  end
end
