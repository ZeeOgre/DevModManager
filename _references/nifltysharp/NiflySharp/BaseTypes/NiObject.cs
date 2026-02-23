using NiflySharp.Helpers;
using NiflySharp.Stream;
using System.Collections.Generic;

namespace NiflySharp
{
    public abstract class NiObject : INiObject
    {
        protected int blockSize = 0;
        protected uint groupId = 0;

        public virtual IEnumerable<INiRef> References
        {
            get
            {
                return [];
            }
        }

        public virtual IEnumerable<INiRef> Pointers
        {
            get
            {
                return [];
            }
        }

        public virtual IEnumerable<NiRefArray> ReferenceArrays
        {
            get
            {
                return [];
            }
        }

        public virtual IEnumerable<NiStringRef> StringRefs
        {
            get
            {
                return [];
            }
        }

        public void BeforeSync(NiStreamReversible stream) { }
        public void AfterSync(NiStreamReversible stream) { }

        public virtual void Sync(NiStreamReversible stream)
        {
            if (stream.Version.FileVersion >= NiFileVersion.V10_0_0_0 && stream.Version.FileVersion < NiFileVersion.V10_1_0_114)
                stream.Sync(ref groupId);
        }

        public object Clone()
        {
            // FIXME: Use deep copy function with reflection for now.
            // Means not having to generate a more specific clone function into each type.
            return DeepCopyHelper.DeepCopy(this);
        }
    }
}
