object FrameAdjustTransform: TFrameAdjustTransform
  Left = 0
  Top = 0
  Width = 471
  Height = 264
  TabOrder = 0
  object Label1: TLabel
    Left = 16
    Top = 133
    Width = 57
    Height = 15
    Caption = 'Translation'
  end
  object Label2: TLabel
    Left = 16
    Top = 160
    Width = 45
    Height = 15
    Caption = 'Rotation'
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 471
    Height = 41
    Align = alTop
    AutoSize = False
    Caption = 
      'Adjust transformation of the root node or nodes by name ("Name" ' +
      'field matches or contains provided word(s), case insensitive). L' +
      'eave values you don'#39't need to update empty.'
    TabOrder = 0
  end
  object edNames: TLabeledEdit
    Left = 16
    Top = 64
    Width = 337
    Height = 23
    EditLabel.Width = 408
    EditLabel.Height = 15
    EditLabel.Caption = 
      'Names, separated by comma if several. Leave empty to update the ' +
      'root node.'
    TabOrder = 1
    Text = ''
  end
  object chkExactMatch: TCheckBox
    Left = 359
    Top = 66
    Width = 89
    Height = 17
    Hint = 
      'Should block names match names exactly or partially (contain the' +
      'm)'
    Caption = 'Exact match'
    Checked = True
    ParentShowHint = False
    ShowHint = True
    State = cbChecked
    TabOrder = 2
  end
  object edPosX: TLabeledEdit
    Left = 112
    Top = 130
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
  object edPosY: TLabeledEdit
    Left = 200
    Top = 130
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
  object edPosZ: TLabeledEdit
    Left = 288
    Top = 130
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
  object edRotY: TLabeledEdit
    Left = 112
    Top = 157
    Width = 41
    Height = 23
    EditLabel.Width = 24
    EditLabel.Height = 23
    EditLabel.Caption = 'Yaw:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 6
    Text = ''
  end
  object edRotP: TLabeledEdit
    Left = 200
    Top = 157
    Width = 41
    Height = 23
    EditLabel.Width = 30
    EditLabel.Height = 23
    EditLabel.Caption = 'Pitch:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 7
    Text = ''
  end
  object edRotR: TLabeledEdit
    Left = 288
    Top = 157
    Width = 41
    Height = 23
    EditLabel.Width = 23
    EditLabel.Height = 23
    EditLabel.Caption = 'Roll:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 8
    Text = ''
  end
  object edScale: TLabeledEdit
    Left = 385
    Top = 140
    Width = 41
    Height = 23
    EditLabel.Width = 30
    EditLabel.Height = 23
    EditLabel.Caption = 'Scale:'
    LabelPosition = lpLeft
    LabelSpacing = 4
    TabOrder = 9
    Text = ''
  end
  object rbAdd: TRadioButton
    Left = 16
    Top = 99
    Width = 53
    Height = 17
    Hint = 'Add values to originals'
    Caption = 'Add'
    Checked = True
    ParentShowHint = False
    ShowHint = True
    TabOrder = 10
    TabStop = True
  end
  object rbMult: TRadioButton
    Left = 88
    Top = 99
    Width = 65
    Height = 17
    Hint = 'Multiply originals by values'
    Caption = 'Multiply'
    ParentShowHint = False
    ShowHint = True
    TabOrder = 11
  end
  object rbSet: TRadioButton
    Left = 168
    Top = 99
    Width = 53
    Height = 17
    Hint = 'Replace originals with values'
    Caption = 'Set'
    ParentShowHint = False
    ShowHint = True
    TabOrder = 12
  end
end
