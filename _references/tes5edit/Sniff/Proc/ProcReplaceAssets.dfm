object FrameReplaceAssets: TFrameReplaceAssets
  Left = 0
  Top = 0
  Width = 517
  Height = 271
  TabOrder = 0
  DesignSize = (
    517
    271)
  object Label1: TLabel
    Left = 16
    Top = 135
    Width = 487
    Height = 33
    Anchors = [akLeft, akTop, akRight]
    AutoSize = False
    Caption = 
      'Replacement pair(s): odd lines specify what text to replace, eve' +
      'n lines text to replace with. If odd line is empty then replacem' +
      'ent text is prepended.'
    WordWrap = True
    ExplicitWidth = 449
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 517
    Height = 65
    Align = alTop
    AutoSize = False
    Caption = 
      'Search and replace assets (textures, materials, etc. ) in NIF me' +
      'shes and FO4 BGSM/BGEM material files. Replacement pairs can be ' +
      'regular expressions, backreferences in replacements like \0, \1,' +
      ' \2 etc. as well as $1, $2, $3, etc. will be substituted with ma' +
      'tched groups. Searching is always case insensitive.'
    TabOrder = 0
  end
  object memoPairs: TMemo
    Left = 16
    Top = 168
    Width = 487
    Height = 100
    Anchors = [akLeft, akTop, akRight, akBottom]
    Lines.Strings = (
      'textures\old\'
      'textures\new\')
    ScrollBars = ssVertical
    TabOrder = 1
    WordWrap = False
  end
  object chkFixAbsolute: TCheckBox
    Left = 16
    Top = 71
    Width = 487
    Height = 17
    Anchors = [akLeft, akTop, akRight]
    Caption = 
      'Fix absolute paths. Truncate up to and including "Data\" ("Data ' +
      'Files\" for Morrowind)'
    TabOrder = 2
  end
  object chkReport: TCheckBox
    Left = 16
    Top = 112
    Width = 257
    Height = 17
    Caption = 'Report only, don'#39't save anything'
    TabOrder = 3
  end
  object chkRegExp: TCheckBox
    Left = 16
    Top = 91
    Width = 257
    Height = 17
    Caption = 'Regular expressions (PCRE)'
    TabOrder = 4
  end
end
