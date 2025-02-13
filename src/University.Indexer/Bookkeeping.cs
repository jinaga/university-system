using Jinaga;
using University.Model;

namespace University.Indexer;

[FactType("SearchIndex.Record")]
public record SearchIndexRecord(Offering offering, string recordId);

[FactType("SearchIndex.Record.InstructorUpdate")]
public record SearchIndexRecordInstructorUpdate(SearchIndexRecord record, OfferingInstructor instructor);

[FactType("SearchIndex.Record.LocationUpdate")]
public record SearchIndexRecordLocationUpdate(SearchIndexRecord record, OfferingLocation location);

[FactType("SearchIndex.Record.TimeUpdate")]
public record SearchIndexRecordTimeUpdate(SearchIndexRecord record, OfferingTime time);
