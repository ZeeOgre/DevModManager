object FrameRemoveNodes: TFrameRemoveNodes
  Left = 0
  Top = 0
  Width = 539
  Height = 337
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 539
    Height = 66
    Align = alTop
    AutoSize = False
    Caption = 
      'Remove nodes (with children) matched by name or type. Be extra c' +
      'autios when removing particle systems, anim contol managers or o' +
      'ther similar deep complex branches. This might leave dangling li' +
      'nks in object palettes, bogus nodes and other issues causing mes' +
      'h to not work without following manual checkup and fixing.'
    TabOrder = 0
  end
  object edNames: TLabeledEdit
    Left = 40
    Top = 108
    Width = 337
    Height = 23
    EditLabel.Width = 203
    EditLabel.Height = 15
    EditLabel.Caption = 'Names, separated by comma if several'
    TabOrder = 1
    Text = ''
  end
  object chkExactMatch: TCheckBox
    Left = 383
    Top = 110
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
  object rbName: TRadioButton
    Left = 16
    Top = 72
    Width = 81
    Height = 17
    Caption = 'by name'
    TabOrder = 3
  end
  object rbType: TRadioButton
    Left = 16
    Top = 139
    Width = 81
    Height = 17
    Caption = 'by type'
    TabOrder = 4
  end
  object cmbType: TComboBox
    Left = 40
    Top = 162
    Width = 337
    Height = 23
    Style = csDropDownList
    DropDownCount = 20
    TabOrder = 5
  end
end
