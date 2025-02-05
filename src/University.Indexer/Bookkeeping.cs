using Jinaga;
using University.Model;

namespace University.Indexer;

[FactType("SearchIndex.Record")]
public record SearchIndexRecord(Offering offering, Guid recordId);
