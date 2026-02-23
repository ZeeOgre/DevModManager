using NiflySharp.Blocks;

namespace NiflySharp
{
    public interface INiSkin : INiStreamable, INiObject
    {
        uint NumBones { get; set; }
        NiBlockPtrArray<NiNode> Bones { get; set; }
    }
}
