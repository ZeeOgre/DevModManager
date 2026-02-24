object FrameAddFacialAnim: TFrameAddFacialAnim
  Left = 0
  Top = 0
  Width = 572
  Height = 365
  TabOrder = 0
  DesignSize = (
    572
    365)
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 572
    Height = 49
    Align = alTop
    AutoSize = False
    Caption = 
      'Add facial animation. Each line consists of space separated valu' +
      'es: Priority, Expession/Phoneme/Modifier, Time, Intensity (Time ' +
      'and Intensity pairs can repeat). https://geckwiki.com/index.php?' +
      'title=ModifyFaceGen'
    TabOrder = 0
  end
  object chkRemove: TCheckBox
    Left = 16
    Top = 66
    Width = 257
    Height = 16
    Caption = 'Remove existing facial animations'
    TabOrder = 1
  end
  object memoMods: TMemo
    Left = 16
    Top = 88
    Width = 537
    Height = 274
    Anchors = [akLeft, akTop, akRight, akBottom]
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWindowText
    Font.Height = -11
    Font.Name = 'Courier New'
    Font.Style = []
    Lines.Strings = (
      '99 Aah 0.466667 0 1.499999 1'
      '99 Eh 1.500000 1'
      '99 BigAah 1.5 1 1.833333 0.1 3.333333 0.5 4.033333 1')
    ParentFont = False
    ScrollBars = ssBoth
    TabOrder = 2
    WordWrap = False
  end
end
