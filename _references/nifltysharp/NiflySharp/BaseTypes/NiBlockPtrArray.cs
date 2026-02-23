using System.Collections.Generic;

namespace NiflySharp
{
    public class NiBlockPtrArray<T> : NiBlockRefArray<T> where T : NiObject
    {
        public NiBlockPtrArray()
        {
        }

        // Don't remove this constructor (required for reflection)
        public NiBlockPtrArray(List<NiBlockRef<T>> refs)
        {
            _refs = refs;
        }

        public IEnumerable<INiRef> Pointers
        {
            get
            {
                return _refs;
            }
        }
    }
}
