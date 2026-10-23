using System.Collections.Generic;

namespace PassAR
{
    /// <summary>Where finished sessions are kept. Swap the implementation to change storage.</summary>
    public interface IDataStore
    {
        void Save(SessionRecord record);
        List<SessionRecord> LoadAll();
        void Clear();
    }
}
