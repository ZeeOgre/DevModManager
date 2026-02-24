object FrameUniversalTweaker: TFrameUniversalTweaker
  Left = 0
  Top = 0
  Width = 475
  Height = 280
  Hint = 
    'When checked also process descendants of specified block types, ' +
    'for example for NiNode it would be BSFadeNode, etc.'
  TabOrder = 0
  DesignSize = (
    475
    280)
  object Label1: TLabel
    Left = 16
    Top = 23
    Width = 443
    Height = 46
    Anchors = [akLeft, akTop, akRight]
    AutoSize = False
    Caption = 
      'Comma separated block types to process. Or a path to the block u' +
      'sing types or names, for example "\BSFadeNode\arms2:2\NiAlphaPro' +
      'perty". When empty processes all blocks. Not used for material f' +
      'iles.'
    WordWrap = True
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 475
    Height = 17
    Align = alTop
    AutoSize = False
    Caption = 
      'Change any field in defined block(s) of nif/kf and FO4 material ' +
      'files. '
    TabOrder = 0
  end
  object edPath: TLabeledEdit
    Left = 16
    Top = 119
    Width = 247
    Height = 23
    Hint = 
      'Path examples: scale value in transformation structure "Transfor' +
      'm\Scale", first texture in texture set "Textures\[0]". You can c' +
      'heck field names by converting to JSON format using Convert to J' +
      'SON operation.'
    EditLabel.Width = 84
    EditLabel.Height = 15
    EditLabel.Caption = 'Path to the field'
    ParentShowHint = False
    ShowHint = True
    TabOrder = 1
    Text = 'Alpha'
    OnChange = edPathChange
  end
  object edValue: TLabeledEdit
    Left = 382
    Top = 119
    Width = 77
    Height = 23
    Anchors = [akLeft, akTop, akRight]
    EditLabel.Width = 28
    EditLabel.Height = 15
    EditLabel.Caption = 'Value'
    TabOrder = 2
    Text = '0.8'
  end
  object chkOldValueCheck: TCheckBox
    Left = 16
    Top = 148
    Width = 313
    Height = 17
    Caption = 'If another field (empty for the same field)'
    TabOrder = 3
    OnClick = chkOldValueCheckClick
  end
  object cmbOldValueMode: TComboBox
    Left = 269
    Top = 171
    Width = 107
    Height = 23
    Style = csDropDownList
    DropDownCount = 20
    TabOrder = 4
    Items.Strings = (
      '='
      '<>'
      '>'
      '<'
      'Contains'
      'Doesn'#39't contain'
      'Starts with'
      'Ends with'
      'AND &'
      'AND NOT &!'
      'Regular Expr')
  end
  object edOldValue: TEdit
    Left = 382
    Top = 171
    Width = 77
    Height = 23
    Anchors = [akLeft, akTop, akRight]
    TabOrder = 5
  end
  object cmbNewValueMode: TComboBox
    Left = 269
    Top = 119
    Width = 107
    Height = 23
    Style = csDropDownList
    DropDownCount = 20
    TabOrder = 6
    Items.Strings = (
      'Set'
      'Add'
      'Mul'
      'Replace with'
      'Prepend'
      'Append'
      'AND &'
      'AND NOT &!'
      'OR |')
  end
  object edBlocks: TEdit
    Left = 16
    Top = 75
    Width = 217
    Height = 23
    Anchors = [akLeft, akTop, akRight]
    TabOrder = 7
    Text = 'NiMaterialProperty'
  end
  object chkInherited: TCheckBox
    Left = 239
    Top = 78
    Width = 121
    Height = 17
    Hint = 
      'When checked also process descendants of specified block types, ' +
      'for example for NiNode it would be BSFadeNode, BSLeadAnimNode, B' +
      'SOrderedNode, etc.'
    Anchors = [akTop, akRight]
    Caption = 'and descendants'
    ParentShowHint = False
    ShowHint = True
    TabOrder = 8
  end
  object chkReport: TCheckBox
    Left = 16
    Top = 200
    Width = 257
    Height = 17
    Caption = 'Report only, don'#39't save anything'
    TabOrder = 9
  end
  object edOldPath: TEdit
    Left = 16
    Top = 171
    Width = 247
    Height = 23
    TabOrder = 10
  end
  object btnPreset: TButton
    Left = 366
    Top = 75
    Width = 93
    Height = 25
    Anchors = [akTop, akRight]
    Caption = 'Presets'
    DropDownMenu = menuPreset
    Style = bsSplitButton
    TabOrder = 11
    TabStop = False
    OnClick = btnPresetClick
  end
  object DefaultPresets: TStaticText
    Left = 296
    Top = 200
    Width = 50
    Height = 19
    AutoSize = False
    Caption = 
      '{"Change body part in BSDismemberSkinInstance partitions":{"sBlo' +
      'cks":"BSDismemberSkinInstance","sDescendants":false,"sPath":"Par' +
      'titions\\[*]\\Body Part","iValueMode":0,"sValue":"SBP_32_BODY","' +
      'bOldValueCheck":true,"sOldPath":"","iOldValueMode":0,"sOldValue"' +
      ':"SBP_34_FOREARMS"},"Set normal texture to diffuse with _n suffi' +
      'x in BSShaderTextureSet":{"sBlocks":"BSShaderTextureSet","sDesce' +
      'ndants":false,"sPath":"Textures\\[1]","iValueMode":3,"sValue":"$' +
      '1_n.dds","bOldValueCheck":true,"sOldPath":"Textures\\[0]","iOldV' +
      'alueMode":10,"sOldValue":"(.+)\\.dds"},"Change Author field in N' +
      'iHeader":{"sBlocks":"NiHeader","sDescendants":false,"sPath":"Exp' +
      'ort Info\\Author","iValueMode":0,"sValue":"Sniff","bOldValueChec' +
      'k":false,"sOldPath":"","iOldValueMode":0,"sOldValue":""},"Add Hi' +
      'dden flag to EditorMarker nodes":{"sBlocks":"NiAVObject","sDesce' +
      'ndants":true,"sPath":"Flags","iValueMode":8,"sValue":"1","bOldVa' +
      'lueCheck":true,"sOldPath":"Name","iOldValueMode":4,"sOldValue":"' +
      'EditorMarker"},"Switch to Parallax shader in BSLightingShaderPro' +
      'perty if there is parallax texture":{"sBlocks":"BSLightingShader' +
      'Property","sDescendants":false,"sPath":"Shader Type","iValueMode' +
      '":0,"sValue":"Parallax","bOldValueCheck":true,"sOldPath":"Textur' +
      'e Set\\Textures\\[3]","iOldValueMode":4,"sOldValue":".dds"},"Add' +
      ' Glow_Map flag if shader is Glow Shader in BSLightingShaderPrope' +
      'rty":{"sBlocks":"BSLightingShaderProperty","sDescendants":false,' +
      '"sPath":"Shader Flags 2","iValueMode":5,"sValue":"| Glow_Map","b' +
      'OldValueCheck":true,"sOldPath":"Shader Type","iOldValueMode":0,"' +
      'sOldValue":"Glow Shader"}}'
    TabOrder = 12
    Visible = False
  end
  object menuPreset: TPopupMenu
    AutoHotkeys = maManual
    Left = 416
    Top = 24
    object miPresetAdd: TMenuItem
      Caption = 'Add'
      OnClick = miPresetAddClick
    end
    object miPresetRemove: TMenuItem
      Caption = 'Remove'
      OnClick = miPresetRemoveClick
    end
    object N1: TMenuItem
      Caption = '-'
    end
  end
end
