object FrameAddLODNode: TFrameAddLODNode
  Left = 0
  Top = 0
  Width = 635
  Height = 404
  TabOrder = 0
  object Label1: TLabel
    Left = 16
    Top = 35
    Width = 94
    Height = 15
    Caption = 'LOD Level Extents'
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 635
    Height = 25
    Align = alTop
    AutoSize = False
    Caption = 
      'Move NiTriStrips and NiTriShape from the root node to the NiLODN' +
      'ode (added if missing)'
    TabOrder = 0
  end
  object memoExtents: TMemo
    Left = 16
    Top = 56
    Width = 121
    Height = 73
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWindowText
    Font.Height = -11
    Font.Name = 'Courier New'
    Font.Style = []
    Lines.Strings = (
      '2000'
      '50000')
    ParentFont = False
    TabOrder = 1
    WordWrap = False
  end
end
