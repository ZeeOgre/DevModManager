object FrameFindSeveralStrips: TFrameFindSeveralStrips
  Left = 0
  Top = 0
  Width = 458
  Height = 283
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 458
    Height = 41
    Align = alTop
    AutoSize = False
    Caption = 
      'Find meshes with NiTriStripsData where the number of strips is g' +
      'reater than the defined value. Extra strips cause excessive draw' +
      ' calls and performance issues.'
    TabOrder = 0
  end
  object edStripsNum: TLabeledEdit
    Left = 16
    Top = 64
    Width = 97
    Height = 23
    EditLabel.Width = 59
    EditLabel.Height = 15
    EditLabel.Caption = 'Strips Num'
    TabOrder = 1
    Text = '1'
  end
end
