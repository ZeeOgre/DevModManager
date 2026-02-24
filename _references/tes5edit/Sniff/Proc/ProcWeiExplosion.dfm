object FrameWeiExplosion: TFrameWeiExplosion
  Left = 0
  Top = 0
  Width = 444
  Height = 225
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 444
    Height = 225
    Align = alClient
    Caption = 
      'NIF postprocessing for blowing up animation. Add all root NiNode' +
      's as children to the NiNode which name ends with "NonAccum". Als' +
      'o add them to Extra Targets of NiMultiTargetTransformController ' +
      'and NiDefaultAVObjectPalette. Create controlled blocks for them ' +
      'in NiControllerSequence. Remove their controllers if any.'
    TabOrder = 0
  end
end
