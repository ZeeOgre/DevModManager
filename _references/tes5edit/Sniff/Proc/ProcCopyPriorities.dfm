object FrameCopyPriorities: TFrameCopyPriorities
  Left = 0
  Top = 0
  Width = 545
  Height = 365
  TabOrder = 0
  DesignSize = (
    545
    365)
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 545
    Height = 49
    Align = alTop
    AutoSize = False
    Caption = 
      'Copy priorities of matched Controlled Blocks in NiControllerSequ' +
      'ence. For each file in the Input directory priorities will be co' +
      'pied from the file with the same path and name in the provided S' +
      'ource directory if exists.'
    TabOrder = 0
  end
  object edSourceDirectory: TLabeledEdit
    Left = 16
    Top = 80
    Width = 401
    Height = 23
    Anchors = [akLeft, akTop, akRight]
    EditLabel.Width = 257
    EditLabel.Height = 15
    EditLabel.Caption = 'Source directory with files to copy priorities from'
    TabOrder = 1
    Text = ''
  end
  object btnBrowse: TButton
    Left = 423
    Top = 78
    Width = 45
    Height = 25
    Anchors = [akTop, akRight]
    Caption = '...'
    TabOrder = 2
    OnClick = btnBrowseClick
  end
end
