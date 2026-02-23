using System;
using System.Collections.Generic;

namespace NiflySharp
{
    public interface INiObject : ICloneable
    {
        IEnumerable<INiRef> References { get; }
        IEnumerable<INiRef> Pointers { get; }
        IEnumerable<NiRefArray> ReferenceArrays { get; }
        IEnumerable<NiStringRef> StringRefs { get; }
    }
}
