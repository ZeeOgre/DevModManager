object FrameAnalyzeMesh: TFrameAnalyzeMesh
  Left = 0
  Top = 0
  Width = 540
  Height = 305
  TabOrder = 0
  object lblInfoMetrics: TLabel
    Left = 16
    Top = 25
    Width = 153
    Height = 15
    Cursor = crHandPoint
    Caption = 'More information on metrics'
    Color = clHighlight
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWindowText
    Font.Height = -12
    Font.Name = 'Segoe UI'
    Font.Style = [fsUnderline]
    ParentColor = False
    ParentFont = False
    OnClick = lblInfoMetricsClick
  end
  object lblInfoCache: TLabel
    Left = 16
    Top = 85
    Width = 352
    Height = 15
    Cursor = crHandPoint
    Caption = 'More information on cache size ("Optimize mesh" uses TomF algo)'
    Color = clHighlight
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWindowText
    Font.Height = -12
    Font.Name = 'Segoe UI'
    Font.Style = [fsUnderline]
    ParentColor = False
    ParentFont = False
    OnClick = lblInfoCacheClick
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 540
    Height = 19
    Align = alTop
    Caption = 'Vertex cache statistics using MethOptimizer library'
    TabOrder = 0
  end
  object edCacheSize: TLabeledEdit
    Left = 16
    Top = 56
    Width = 33
    Height = 23
    EditLabel.Width = 114
    EditLabel.Height = 23
    EditLabel.Caption = 'GPU Vertex cache size'
    LabelPosition = lpRight
    TabOrder = 1
    Text = '16'
  end
  object edACMR: TLabeledEdit
    Left = 16
    Top = 141
    Width = 33
    Height = 23
    EditLabel.Width = 34
    EditLabel.Height = 23
    EditLabel.Caption = 'ACMR'
    LabelPosition = lpRight
    TabOrder = 2
    Text = '1.5'
  end
  object edATVR: TLabeledEdit
    Left = 112
    Top = 141
    Width = 33
    Height = 23
    EditLabel.Width = 27
    EditLabel.Height = 23
    EditLabel.Caption = 'ATVR'
    LabelPosition = lpRight
    TabOrder = 3
    Text = '1.5'
  end
  object chkThreshold: TCheckBox
    Left = 16
    Top = 118
    Width = 257
    Height = 17
    Caption = 'Don'#39't output values below threshold'
    Checked = True
    State = cbChecked
    TabOrder = 4
  end
  object chkPerShape: TCheckBox
    Left = 16
    Top = 182
    Width = 153
    Height = 17
    Caption = 'Per shape statistics'
    TabOrder = 5
  end
  object edVertices: TLabeledEdit
    Left = 208
    Top = 141
    Width = 49
    Height = 23
    EditLabel.Width = 40
    EditLabel.Height = 23
    EditLabel.Caption = 'Vertices'
    LabelPosition = lpRight
    TabOrder = 6
    Text = ''
  end
end
