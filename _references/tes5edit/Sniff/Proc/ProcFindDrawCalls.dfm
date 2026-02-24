object FrameFindDrawCalls: TFrameFindDrawCalls
  Left = 0
  Top = 0
  Width = 481
  Height = 287
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 481
    Height = 17
    Align = alTop
    AutoSize = False
    Caption = 'Find meshes with excessive amount of draw calls'
    TabOrder = 0
  end
  object edCallsNum: TLabeledEdit
    Left = 16
    Top = 48
    Width = 97
    Height = 23
    EditLabel.Width = 106
    EditLabel.Height = 15
    EditLabel.Caption = 'Draw calls threshold'
    TabOrder = 1
    Text = '10'
  end
end
