object FrameSoftParticles: TFrameSoftParticles
  Left = 0
  Top = 0
  Width = 543
  Height = 336
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 543
    Height = 49
    Align = alTop
    AutoSize = False
    Caption = 
      'Intended to be used with Vanilla Plus Particles - NVSE. Set soft' +
      ' particles (Unknown9) flag in BSShaderNoLightingProperty. Option' +
      'ally add NiFloatExtraData named "VPSoftScale" with defined value' +
      '.'
    TabOrder = 0
    ExplicitLeft = -24
    ExplicitWidth = 567
  end
  object edSoftScale: TLabeledEdit
    Left = 16
    Top = 80
    Width = 177
    Height = 23
    EditLabel.Width = 153
    EditLabel.Height = 15
    EditLabel.Caption = 'Soft scale (empty to not add)'
    TabOrder = 1
    Text = '0.05'
  end
end
