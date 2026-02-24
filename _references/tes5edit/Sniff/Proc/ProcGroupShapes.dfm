object FrameGroupShapes: TFrameGroupShapes
  Left = 0
  Top = 0
  Width = 496
  Height = 267
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 496
    Height = 33
    Align = alTop
    AutoSize = False
    Caption = 
      'Find BSTriShape, NiTriShape and NiTriStrips in root node childre' +
      'n which use the same diffuse texture and put them together under' +
      ' a newly created NiNode.'
    TabOrder = 0
  end
  object chkSplit: TCheckBox
    Left = 16
    Top = 47
    Width = 465
    Height = 17
    Caption = 
      'Split into multiple NiNodes based on 65536 vertices and tris lim' +
      'it per node'
    TabOrder = 1
  end
  object chkAllFeatures: TCheckBox
    Left = 16
    Top = 70
    Width = 465
    Height = 17
    Caption = 
      'Compare all textures (diffuse, normal, cubemap, etc), not just d' +
      'iffuse only'
    TabOrder = 2
  end
end
