object FrameUniversalFixer: TFrameUniversalFixer
  Left = 0
  Top = 0
  Width = 557
  Height = 346
  TabOrder = 0
  DesignSize = (
    557
    346)
  object memoFix: TMemo
    AlignWithMargins = True
    Left = 0
    Top = 90
    Width = 557
    Height = 256
    Margins.Left = 0
    Margins.Top = 90
    Margins.Right = 0
    Margins.Bottom = 0
    Align = alClient
    BorderStyle = bsNone
    ReadOnly = True
    ScrollBars = ssVertical
    TabOrder = 0
    WantReturns = False
  end
  object chkSaveLog: TCheckBox
    Left = 11
    Top = 11
    Width = 142
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'Save changes log to'
    TabOrder = 1
  end
  object edLog: TEdit
    Left = 40
    Top = 34
    Width = 457
    Height = 23
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Anchors = [akLeft, akTop, akRight]
    ReadOnly = True
    TabOrder = 2
  end
  object btnBrowse: TButton
    Left = 503
    Top = 33
    Width = 45
    Height = 25
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Anchors = [akTop, akRight]
    Caption = '...'
    TabOrder = 3
    OnClick = btnBrowseClick
  end
  object chkOpenLog: TCheckBox
    Left = 40
    Top = 63
    Width = 142
    Height = 17
    Margins.Left = 5
    Margins.Top = 5
    Margins.Right = 5
    Margins.Bottom = 5
    Caption = 'Open log when done'
    TabOrder = 4
  end
end
