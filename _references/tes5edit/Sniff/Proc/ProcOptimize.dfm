object FrameOptimize: TFrameOptimize
  Left = 0
  Top = 0
  Width = 548
  Height = 284
  TabOrder = 0
  DesignSize = (
    548
    284)
  object Label1: TLabel
    Left = 32
    Top = 60
    Width = 382
    Height = 15
    Caption = 
      'Convert NiTriStrips to NiTriShape and strips to triangles in NiS' +
      'kinPartition'
  end
  object Label2: TLabel
    Left = 32
    Top = 102
    Width = 497
    Height = 36
    Anchors = [akLeft, akTop, akRight]
    AutoSize = False
    Caption = 
      'Convert NiTriShape to NiTriStrips and triangles to strips in NiS' +
      'kinPartition. Restripify existing strips if Num Strips > 1'
    WordWrap = True
  end
  object lblInfo: TLabel
    Left = 231
    Top = 169
    Width = 94
    Height = 15
    Cursor = crHandPoint
    Caption = 'More information'
    Color = clHighlight
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWindowText
    Font.Height = -12
    Font.Name = 'Segoe UI'
    Font.Style = [fsUnderline]
    ParentColor = False
    ParentFont = False
    OnClick = lblInfoClick
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 548
    Height = 33
    Align = alTop
    AutoSize = False
    Caption = 
      'Apply selected optimizations from MeshOptimizer library. Warning' +
      ': Vertex fetch optimization reorders vertices and will break mes' +
      'hes dependant on their order (disabled for skinned shapes).'
    TabOrder = 0
  end
  object chkTriangulate: TCheckBox
    Left = 16
    Top = 39
    Width = 273
    Height = 17
    Caption = 'Triangulate'
    TabOrder = 1
    OnClick = chkTriangulateClick
  end
  object chkStripify: TCheckBox
    Left = 16
    Top = 81
    Width = 273
    Height = 17
    Caption = 'Stripify'
    TabOrder = 2
    OnClick = chkStripifyClick
  end
  object chkVertexCache: TCheckBox
    Left = 16
    Top = 146
    Width = 201
    Height = 17
    Caption = 'Vertex cache optimization'
    Checked = True
    State = cbChecked
    TabOrder = 3
  end
  object chkVertexFetch: TCheckBox
    Left = 16
    Top = 192
    Width = 201
    Height = 17
    Caption = 'Vertex fetch optimization'
    Checked = True
    State = cbChecked
    TabOrder = 4
  end
  object chkOverdraw: TCheckBox
    Left = 16
    Top = 169
    Width = 201
    Height = 17
    Caption = 'Overdraw optimization'
    Checked = True
    State = cbChecked
    TabOrder = 5
  end
end
