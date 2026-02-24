object FrameRenameStrings: TFrameRenameStrings
  Left = 0
  Top = 0
  Width = 482
  Height = 333
  TabOrder = 0
  DesignSize = (
    482
    333)
  object Label1: TLabel
    Left = 16
    Top = 62
    Width = 449
    Height = 33
    Anchors = [akLeft, akTop, akRight]
    AutoSize = False
    Caption = 
      'Replacement pair(s): odd lines specify what text to replace, eve' +
      'n lines text to replace with.'
    WordWrap = True
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 482
    Height = 33
    Align = alTop
    AutoSize = False
    Caption = 
      'Search and replace in the Strings list in NiHeader. Searching is' +
      ' case insensitive, unused strings will be removed.'
    TabOrder = 0
  end
  object chkExactMatch: TCheckBox
    Left = 16
    Top = 39
    Width = 153
    Height = 17
    Hint = 'Match against the full string instead of partially'
    Caption = 'Full string match'
    ParentShowHint = False
    ShowHint = True
    TabOrder = 1
  end
  object chkTrim: TCheckBox
    Left = 200
    Top = 39
    Width = 153
    Height = 17
    Hint = 'Trim left and right whitespaces if any after replacement'
    Caption = 'Trim whitespaces'
    Checked = True
    ParentShowHint = False
    ShowHint = True
    State = cbChecked
    TabOrder = 2
  end
  object memoPairs: TMemo
    Left = 16
    Top = 96
    Width = 449
    Height = 225
    Anchors = [akLeft, akTop, akRight, akBottom]
    Lines.Strings = (
      'oldstring'
      'newstring'
      'otherold'
      'othernew'
      ''
      '')
    ScrollBars = ssVertical
    TabOrder = 3
    WordWrap = False
  end
end
