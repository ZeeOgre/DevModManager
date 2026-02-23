using NiflySharp.Structs;
using System.Numerics;

namespace NiflySharp.Blocks
{
    public partial class NiNode : NiAVObject
    {
        public NiNode()
        {
            Children = new NiBlockRefArray<NiAVObject>();
            Effects = new NiBlockRefArray<NiDynamicEffect>();
        }

        public MatTransform TransformToParent
        {
            get
            {
                return new MatTransform()
                {
                    Translation = Translation,
                    Rotation = new Matrix3(
                        new Vector3(Rotation.M11, Rotation.M12, Rotation.M13),
                        new Vector3(Rotation.M21, Rotation.M22, Rotation.M23),
                        new Vector3(Rotation.M31, Rotation.M32, Rotation.M33)),
                    Scale = Scale
                };
            }
            set
            {
                Translation = value.Translation;
                Rotation = new Matrix33()
                {
                    M11 = value.Rotation.Rows[0].X,
                    M12 = value.Rotation.Rows[0].Y,
                    M13 = value.Rotation.Rows[0].Z,
                    M21 = value.Rotation.Rows[1].X,
                    M22 = value.Rotation.Rows[1].Y,
                    M23 = value.Rotation.Rows[1].Z,
                    M31 = value.Rotation.Rows[2].X,
                    M32 = value.Rotation.Rows[2].Y,
                    M33 = value.Rotation.Rows[2].Z
                };
                Scale = value.Scale;
            }
        }
    }
}
