object FrameFindUVs: TFrameFindUVs
  Left = 0
  Top = 0
  Width = 540
  Height = 322
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 540
    Height = 25
    Align = alTop
    AutoSize = False
    Caption = 
      'Find meshes with UVs lesser or greater than defined values. Empt' +
      'y means don'#39't check.'
    TabOrder = 0
  end
  object edUMin: TLabeledEdit
    Left = 48
    Top = 40
    Width = 65
    Height = 23
    EditLabel.Width = 19
    EditLabel.Height = 23
    EditLabel.Caption = 'U <'
    LabelPosition = lpLeft
    TabOrder = 1
    Text = '0'
  end
  object edUMax: TLabeledEdit
    Left = 160
    Top = 40
    Width = 65
    Height = 23
    EditLabel.Width = 19
    EditLabel.Height = 23
    EditLabel.Caption = 'U >'
    LabelPosition = lpLeft
    TabOrder = 2
    Text = ''
  end
  object edVMin: TLabeledEdit
    Left = 48
    Top = 69
    Width = 65
    Height = 23
    EditLabel.Width = 18
    EditLabel.Height = 23
    EditLabel.Caption = 'V <'
    LabelPosition = lpLeft
    TabOrder = 3
    Text = '0'
  end
  object edVMax: TLabeledEdit
    Left = 160
    Top = 69
    Width = 65
    Height = 23
    EditLabel.Width = 18
    EditLabel.Height = 23
    EditLabel.Caption = 'V >'
    LabelPosition = lpLeft
    TabOrder = 4
    Text = ''
  end
end
