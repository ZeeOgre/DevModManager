object FrameInertiaUpdate: TFrameInertiaUpdate
  Left = 0
  Top = 0
  Width = 526
  Height = 329
  TabOrder = 0
  DesignSize = (
    526
    329)
  object Label1: TLabel
    Left = 16
    Top = 72
    Width = 169
    Height = 15
    Caption = 'Inertia multipliers for body parts'
  end
  object StaticText1: TStaticText
    Left = 0
    Top = 0
    Width = 526
    Height = 65
    Align = alTop
    AutoSize = False
    Caption = 
      'Calculate inertia tensor matrix, collision center and penetratio' +
      'n depth of dynamic Havok rigid bodies with single shape. Inertia' +
      ' multipliers list is editable: "Body Part" field must have an in' +
      'teger value before the space (the rest is ignored), "Multiplier"' +
      ' field has to be a valid float number (empty is equal to 1).'
    TabOrder = 0
    ExplicitWidth = 518
  end
  object chkCenter: TCheckBox
    Left = 241
    Top = 127
    Width = 263
    Height = 17
    Caption = 'Update collision center (dynamic only)'
    Checked = True
    State = cbChecked
    TabOrder = 1
  end
  object edMult: TValueListEditor
    AlignWithMargins = True
    Left = 16
    Top = 96
    Width = 201
    Height = 221
    Margins.Left = 4
    Margins.Top = 0
    Margins.Right = 12
    Margins.Bottom = 0
    Anchors = [akLeft, akTop, akBottom]
    DefaultColWidth = 100
    DisplayOptions = [doColumnTitles, doAutoColResize]
    KeyOptions = [keyEdit, keyAdd, keyDelete, keyUnique]
    Options = [goFixedVertLine, goFixedHorzLine, goVertLine, goHorzLine, goDrawFocusSelected, goEditing, goAlwaysShowEditor, goThumbTracking, goFixedRowClick]
    ScrollBars = ssVertical
    Strings.Strings = (
      '0 Other='
      '1 Head=2'
      '2 Body=3'
      '3 Spine1=3'
      '4 Spine2=3'
      '5 LUpperArm=2'
      '6 LForeArm='
      '7 LHand='
      '8 LThigh=2'
      '9 LCalf='
      '10 LFoot='
      '11 RUpperArm=2'
      '12 RForeArm='
      '13 RHand='
      '14 RThigh=2'
      '15 RCalf='
      '16 RFoot='
      '17 Tail='
      '18 Shield='
      '19 Quiver='
      '20 Weapon='
      '21 PonyTail='
      '22 Wing='
      '23 Pack='
      '24 Chain='
      '25 AddonHead='
      '26 AddonChest='
      '27 AddonArm='
      '28 AddonLeg=')
    TabOrder = 2
    TitleCaptions.Strings = (
      'Body Part'
      'Multiplier')
    ColWidths = (
      100
      78)
  end
  object chkPenetration: TCheckBox
    Left = 241
    Top = 158
    Width = 168
    Height = 17
    Caption = 'Update penetration depth'
    TabOrder = 3
  end
  object chkInertia: TCheckBox
    Left = 241
    Top = 96
    Width = 263
    Height = 17
    Caption = 'Update inertia tensor (dynamic only)'
    Checked = True
    State = cbChecked
    TabOrder = 4
  end
  object edDepthMult: TLabeledEdit
    Left = 264
    Top = 181
    Width = 41
    Height = 23
    EditLabel.Width = 60
    EditLabel.Height = 23
    EditLabel.Caption = 'Depth mult'
    LabelPosition = lpRight
    TabOrder = 5
    Text = '0.2'
  end
  object chkPenetrationStatics: TCheckBox
    Left = 415
    Top = 158
    Width = 108
    Height = 17
    Caption = 'on statics too'
    TabOrder = 6
  end
end
