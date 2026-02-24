object FrameVertexPaint: TFrameVertexPaint
  Left = 0
  Top = 0
  Width = 550
  Height = 296
  TabOrder = 0
  object Label1: TLabel
    Left = 27
    Top = 169
    Width = 111
    Height = 15
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Caption = 'RGBA hex color code'
  end
  object lblColor2: TLabel
    Left = 298
    Top = 169
    Width = 64
    Height = 15
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Caption = 'replace with'
  end
  object lblHelper: TLabel
    Left = 517
    Top = 80
    Width = 21
    Height = 13
    Cursor = crHandPoint
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Caption = 'Help'
    Color = clHighlight
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWindowText
    Font.Height = -11
    Font.Name = 'Tahoma'
    Font.Style = [fsUnderline]
    ParentColor = False
    ParentFont = False
    OnClick = lblHelperClick
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 550
    Height = 33
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Align = alTop
    AutoSize = False
    Caption = 
      'Fill vertex colors in NiTriShapeData and NiTriStripsData with th' +
      'e specified one, adjust with HSL modifiers, or remove completely' +
      '.'
    TabOrder = 0
  end
  object rbSetColors: TRadioButton
    Left = 24
    Top = 48
    Width = 153
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'Set vertex colors'
    Checked = True
    TabOrder = 1
    TabStop = True
    OnClick = rbSetColorsClick
  end
  object rbRemoveColors: TRadioButton
    Left = 24
    Top = 108
    Width = 153
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'Remove vertex colors'
    TabOrder = 2
    OnClick = rbSetColorsClick
  end
  object chkAllWhite: TCheckBox
    Left = 183
    Top = 108
    Width = 290
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'only if all colors are equal to the specified color'
    TabOrder = 3
  end
  object edColor: TEdit
    Left = 163
    Top = 166
    Width = 73
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    TabOrder = 4
    Text = 'FFFFFFFF'
  end
  object btnColorSelect: TButton
    Left = 242
    Top = 165
    Width = 34
    Height = 25
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = '...'
    TabOrder = 5
    OnClick = btnColorSelectClick
  end
  object chkAddIfMissing: TCheckBox
    Left = 183
    Top = 48
    Width = 145
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'add if missing'
    TabOrder = 6
  end
  object rbAdjustColors: TRadioButton
    Left = 24
    Top = 78
    Width = 153
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'Adjust vertex colors'
    TabOrder = 7
    OnClick = rbSetColorsClick
  end
  object cbAdjustMod: TComboBox
    Left = 183
    Top = 76
    Width = 74
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Style = csDropDownList
    ItemIndex = 0
    TabOrder = 8
    Text = 'multiply'
    Items.Strings = (
      'multiply'
      'add')
  end
  object edAdjustH: TLabeledEdit
    Left = 282
    Top = 76
    Width = 36
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    EditLabel.Width = 12
    EditLabel.Height = 23
    EditLabel.Margins.Left = 5
    EditLabel.Margins.Top = 5
    EditLabel.Margins.Right = 5
    EditLabel.Margins.Bottom = 5
    EditLabel.Caption = 'H:'
    LabelPosition = lpLeft
    TabOrder = 9
    Text = ''
  end
  object edAdjustS: TLabeledEdit
    Left = 346
    Top = 76
    Width = 36
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    EditLabel.Width = 9
    EditLabel.Height = 23
    EditLabel.Margins.Left = 5
    EditLabel.Margins.Top = 5
    EditLabel.Margins.Right = 5
    EditLabel.Margins.Bottom = 5
    EditLabel.Caption = 'S:'
    LabelPosition = lpLeft
    TabOrder = 10
    Text = ''
  end
  object edAdjustL: TLabeledEdit
    Left = 410
    Top = 76
    Width = 36
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    EditLabel.Width = 9
    EditLabel.Height = 23
    EditLabel.Margins.Left = 5
    EditLabel.Margins.Top = 5
    EditLabel.Margins.Right = 5
    EditLabel.Margins.Bottom = 5
    EditLabel.Caption = 'L:'
    LabelPosition = lpLeft
    TabOrder = 11
    Text = ''
  end
  object edAdjustA: TLabeledEdit
    Left = 475
    Top = 76
    Width = 36
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    EditLabel.Width = 11
    EditLabel.Height = 23
    EditLabel.Margins.Left = 5
    EditLabel.Margins.Top = 5
    EditLabel.Margins.Right = 5
    EditLabel.Margins.Bottom = 5
    EditLabel.Caption = 'A:'
    LabelPosition = lpLeft
    TabOrder = 12
    Text = ''
  end
  object rbReplaceColor: TRadioButton
    Left = 24
    Top = 137
    Width = 153
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'Replace vertex color'
    TabOrder = 13
    OnClick = rbSetColorsClick
  end
  object edColor2: TEdit
    Left = 373
    Top = 166
    Width = 73
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    TabOrder = 14
    Text = 'FFFFFFFF'
  end
  object btnColor2Select: TButton
    Left = 452
    Top = 165
    Width = 34
    Height = 25
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = '...'
    TabOrder = 15
    OnClick = btnColorSelectClick
  end
end
