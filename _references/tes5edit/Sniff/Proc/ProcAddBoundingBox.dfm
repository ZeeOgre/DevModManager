object FrameAddBoundingBox: TFrameAddBoundingBox
  Left = 0
  Top = 0
  Width = 505
  Height = 328
  TabOrder = 0
  object Label1: TLabel
    Left = 16
    Top = 82
    Width = 35
    Height = 15
    Caption = 'Center'
  end
  object Label2: TLabel
    Left = 16
    Top = 109
    Width = 33
    Height = 15
    Caption = 'Extent'
  end
  object Label3: TLabel
    Left = 16
    Top = 42
    Width = 27
    Height = 15
    Caption = 'Flags'
  end
  object edCenterX: TLabeledEdit
    Left = 80
    Top = 79
    Width = 41
    Height = 23
    EditLabel.Width = 10
    EditLabel.Height = 23
    EditLabel.Caption = 'X:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 0
    Text = ''
  end
  object edCenterY: TLabeledEdit
    Left = 168
    Top = 79
    Width = 41
    Height = 23
    EditLabel.Width = 10
    EditLabel.Height = 23
    EditLabel.Caption = 'Y:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 1
    Text = ''
  end
  object edCenterZ: TLabeledEdit
    Left = 256
    Top = 79
    Width = 41
    Height = 23
    EditLabel.Width = 10
    EditLabel.Height = 23
    EditLabel.Caption = 'Z:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 2
    Text = ''
  end
  object edExtentX: TLabeledEdit
    Left = 80
    Top = 106
    Width = 41
    Height = 23
    EditLabel.Width = 10
    EditLabel.Height = 23
    EditLabel.Caption = 'X:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 3
    Text = ''
  end
  object edExtentY: TLabeledEdit
    Left = 168
    Top = 106
    Width = 41
    Height = 23
    EditLabel.Width = 10
    EditLabel.Height = 23
    EditLabel.Caption = 'Y:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 4
    Text = ''
  end
  object edExtentZ: TLabeledEdit
    Left = 256
    Top = 106
    Width = 41
    Height = 23
    EditLabel.Width = 10
    EditLabel.Height = 23
    EditLabel.Caption = 'Z:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 5
    Text = ''
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 505
    Height = 33
    Align = alTop
    AutoSize = False
    Caption = 
      'Add NiNode named "Bounding Box" to the root node if missing with' +
      ' BOX_BV bounding volume data.'
    TabOrder = 6
  end
  object edFlags: TEdit
    Left = 80
    Top = 39
    Width = 41
    Height = 23
    TabOrder = 7
    Text = '12'
  end
end
