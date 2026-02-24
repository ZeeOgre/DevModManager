object FrameApplyTransform: TFrameApplyTransform
  Left = 0
  Top = 0
  Width = 529
  Height = 287
  TabOrder = 0
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 529
    Height = 33
    Align = alTop
    AutoSize = False
    Caption = 
      'Apply transform on all nodes optionally excluding the ones which' +
      ' can not be transformed without potential issues'
    TabOrder = 0
  end
  object chkSkipSkinned: TCheckBox
    Left = 16
    Top = 39
    Width = 361
    Height = 17
    Caption = 'Skip nodes used as bones or with set Skin field'
    Checked = True
    State = cbChecked
    TabOrder = 1
  end
  object chkSkipAnimated: TCheckBox
    Left = 16
    Top = 62
    Width = 361
    Height = 17
    Caption = 'Skip nodes with set Controller field'
    Checked = True
    State = cbChecked
    TabOrder = 2
  end
  object chkSkipCollision: TCheckBox
    Left = 16
    Top = 85
    Width = 361
    Height = 17
    Caption = 'Skip nodes with set Collision Object field'
    Checked = True
    State = cbChecked
    TabOrder = 3
  end
  object chkSkipRoot: TCheckBox
    Left = 16
    Top = 108
    Width = 361
    Height = 17
    Caption = 'Skip root node if mesh has skin instances and bones'
    Checked = True
    State = cbChecked
    TabOrder = 4
  end
  object chkSkipControllerManager: TCheckBox
    Left = 16
    Top = 131
    Width = 361
    Height = 17
    Caption = 'Skip animated meshes with NiControllerManager'
    Checked = True
    State = cbChecked
    TabOrder = 5
  end
end
