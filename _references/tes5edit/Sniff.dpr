program Sniff;

uses
  MSHeap in 'Core\MSHeap.pas',
  Vcl.Forms,
  Vcl.Themes,
  Vcl.Styles,
  wbCommandLine in 'Core\wbCommandLine.pas',
  wbDataFormat in 'Core\wbDataFormat.pas',
  wbDataFormatMaterial in 'Core\wbDataFormatMaterial.pas',
  wbDataFormatNif in 'Core\wbDataFormatNif.pas',
  wbDataFormatNifTypes in 'Core\wbDataFormatNifTypes.pas',
  wbDataFormatMisc in 'Core\wbDataFormatMisc.pas',
  wbMeshOptimize in 'Core\wbMeshOptimize.pas',
  wbNifMath in 'Core\wbNifMath.pas',
  SniffProcessor in 'Sniff\SniffProcessor.pas',
  frmMain in 'Sniff\frmMain.pas' {FormMain},
  frMessages in 'Sniff\frMessages.pas' {FrameMessages: TFrame},
  frmVertexPaintHelper in 'Sniff\frmVertexPaintHelper.pas' {FormVertexPaintHelper},
  ProcAddBoundingBox in 'Sniff\Proc\ProcAddBoundingBox.pas' {FrameAddBoundingBox: TFrame},
  ProcAddFacialAnim in 'Sniff\Proc\ProcAddFacialAnim.pas' {FrameAddFacialAnim: TFrame},
  ProcAddHeadtrackingAnim in 'Sniff\Proc\ProcAddHeadtrackingAnim.pas' {FrameAddHeadtrackingAnim: TFrame},
  ProcAddLODNode in 'Sniff\Proc\ProcAddLODNode.pas' {FrameAddLODNode: TFrame},
  ProcAddRootCollisionNode in 'Sniff\Proc\ProcAddRootCollisionNode.pas' {FrameAddRootCollisionNode: TFrame},
  ProcAdjustTransform in 'Sniff\Proc\ProcAdjustTransform.pas' {FrameAdjustTransform: TFrame},
  ProcAnalyzeMesh in 'Sniff\Proc\ProcAnalyzeMesh.pas' {FrameAnalyzeMesh: TFrame},
  ProcAnimQuadraticToLinear in 'Sniff\Proc\ProcAnimQuadraticToLinear.pas' {FrameAnimQuadraticToLinear: TFrame},
  ProcAnimSkeletonDeath in 'Sniff\Proc\ProcAnimSkeletonDeath.pas' {FrameAnimSkeletonDeath: TFrame},
  ProcApplyTransform in 'Sniff\Proc\ProcApplyTransform.pas' {FrameApplyTransform: TFrame},
  ProcAttachParent in 'Sniff\Proc\ProcAttachParent.pas' {FrameAttachParent: TFrame},
  ProcChangePartitionSlot in 'Sniff\Proc\ProcChangePartitionSlot.pas' {FrameChangePartitionSlot: TFrame},
  ProcCheckForErrors in 'Sniff\Proc\ProcCheckForErrors.pas' {FrameCheckForErrors: TFrame},
  ProcConvertRootNode in 'Sniff\Proc\ProcConvertRootNode.pas' {FrameConvertRootNode: TFrame},
  ProcCopyControlledBlocks in 'Sniff\Proc\ProcCopyControlledBlocks.pas' {FrameCopyControlledBlocks: TFrame},
  ProcCopyGeometryBlocks in 'Sniff\Proc\ProcCopyGeometryBlocks.pas' {FrameCopyGeometryBlocks: TFrame},
  ProcCopyPriorities in 'Sniff\Proc\ProcCopyPriorities.pas' {FrameCopyPriorities: TFrame},
  ProcFindDrawCalls in 'Sniff\Proc\ProcFindDrawCalls.pas' {FrameFindDrawCalls: TFrame},
  ProcFindSeveralStrips in 'Sniff\Proc\ProcFindSeveralStrips.pas' {FrameFindSeveralStrips: TFrame},
  ProcFindUVs in 'Sniff\Proc\ProcFindUVs.pas' {FrameFindUVs: TFrame},
  ProcFixExportedKFAnim in 'Sniff\Proc\ProcFixExportedKFAnim.pas' {FrameFixExportedKFAnim: TFrame},
  ProcGroupShapes in 'Sniff\Proc\ProcGroupShapes.pas' {FrameGroupShapes: TFrame},
  ProcHavokInfo in 'Sniff\Proc\ProcHavokInfo.pas' {FrameHavokInfo: TFrame},
  ProcHavokSearchMaterial in 'Sniff\Proc\ProcHavokSearchMaterial.pas' {FrameHavokMaterial: TFrame},
  ProcHavokSettingsUpdate in 'Sniff\Proc\ProcHavokSettingsUpdate.pas' {FrameHavokSettings: TFrame},
  ProcInertiaUpdate in 'Sniff\Proc\ProcInertiaUpdate.pas' {Frame1: TFrame},
  ProcJamAnim in 'Sniff\Proc\ProcJamAnim.pas' {FrameJamAnim: TFrame},
  ProcJsonConverter in 'Sniff\Proc\ProcJsonConverter.pas' {FrameJsonConverter: TFrame},
  ProcMergeShapes in 'Sniff\Proc\ProcMergeShapes.pas' {FrameMergeShapes: TFrame},
  ProcMoppUpdate in 'Sniff\Proc\ProcMoppUpdate.pas' {FrameMoppUpdate: TFrame},
  ProcOptimize in 'Sniff\Proc\ProcOptimize.pas' {FrameOptimize: TFrame},
  ProcOptimizeKF in 'Sniff\Proc\ProcOptimizeKF.pas' {FrameOptimizeKF: TFrame},
  ProcPriorityControlledBlocks in 'Sniff\Proc\ProcPriorityControlledBlocks.pas' {FramePriorityControlledBlocks: TFrame},
  ProcRagdollConstraintUpdate in 'Sniff\Proc\ProcRagdollConstraintUpdate.pas' {FrameRagdollConstraintUpdate: TFrame},
  ProcRemoveControlledBlocks in 'Sniff\Proc\ProcRemoveControlledBlocks.pas' {FrameRemoveControlledBlocks: TFrame},
  ProcRemoveNodes in 'Sniff\Proc\ProcRemoveNodes.pas' {FrameRemoveNodes: TFrame},
  ProcRemoveUnusedNodes in 'Sniff\Proc\ProcRemoveUnusedNodes.pas' {FrameRemoveUnusedNodes: TFrame},
  ProcRenameControlledBlocks in 'Sniff\Proc\ProcRenameControlledBlocks.pas' {FrameRenameControlledBlocks: TFrame},
  ProcRenameStrings in 'Sniff\Proc\ProcRenameStrings.pas' {FrameRenameStrings: TFrame},
  ProcReplaceAssets in 'Sniff\Proc\ProcReplaceAssets.pas' {FrameReplaceAssets: TFrame},
  ProcSetMissingNames in 'Sniff\Proc\ProcSetMissingNames.pas' {FrameSetMissingNames: TFrame},
  ProcShaderFlagsUpdate in 'Sniff\Proc\ProcShaderFlagsUpdate.pas' {FrameShaderFlagsUpdate: TFrame},
  ProcSoftParticles in 'Sniff\Proc\ProcSoftParticles.pas' {FrameSoftParticles: TFrame},
  ProcTangents in 'Sniff\Proc\ProcTangents.pas' {FrameTangents: TFrame},
  ProcUniversalFixer in 'Sniff\Proc\ProcUniversalFixer.pas' {FrameUniversalFixer: TFrame},
  ProcUniversalTweaker in 'Sniff\Proc\ProcUniversalTweaker.pas' {FrameUniversalTweaker: TFrame},
  ProcUnskinMesh in 'Sniff\Proc\ProcUnskinMesh.pas' {FrameUnskinMesh: TFrame},
  ProcUnweldedVertices in 'Sniff\Proc\ProcUnweldedVertices.pas' {FrameUnweldedVertices: TFrame},
  ProcUpdateBounds in 'Sniff\Proc\ProcUpdateBounds.pas' {FrameUpdateBounds: TFrame},
  ProcVertexPaint in 'Sniff\Proc\ProcVertexPaint.pas' {FrameVertexPaint: TFrame},
  ProcWallsReflectionFlag in 'Sniff\Proc\ProcWallsReflectionFlag.pas' {FrameWallsReflectionFlag: TFrame},
  ProcWeiExplosion in 'Sniff\Proc\ProcWeiExplosion.pas' {FrameWeiExplosion: TFrame};
  
{$R *.res}

{$DYNAMICBASE ON}
{$SetPEFlags $0020}
begin
  try
  Application.Initialize;
  Application.MainFormOnTaskbar := True;
  Application.CreateForm(TFormMain, FormMain);
  Application.Run;
  except end;
end.
