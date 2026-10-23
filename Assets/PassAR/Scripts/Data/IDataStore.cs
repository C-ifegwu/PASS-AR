using System.Collections.Generic;

namespace PassAR
{
    /// <summary>Where finished results are kept. Another way of saving can be used by swapping this part.</summary>
    public interface IDataStore
    {
        void Save(SessionRecord record);
        List<SessionRecord> LoadAll();
        void Clear();
    }
}
