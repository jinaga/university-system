using Jinaga;
using University.Model;

namespace University.Indexer.Elasticsearch;

public record OfferingIndex(Offering offering, OfferingLocation? location, OfferingTime? time, OfferingInstructor? instructor)
{

    public static OfferingIndex Create(Offering offering)
    {
        return new OfferingIndex(offering, null, null, null);
    }

    public OfferingIndex WithLocation(OfferingLocation location)
    {
        return new OfferingIndex(offering, location, time, instructor);
    }

    public OfferingIndex WithTime(OfferingTime time)
    {
        return new OfferingIndex(offering, location, time, instructor);
    }

    public OfferingIndex WithInstructor(OfferingInstructor instructor)
    {
        return new OfferingIndex(offering, location, time, instructor);
    }

    public SearchRecord GetSearchRecord(string recordId)
    {
        return new SearchRecord
        {
            Id = recordId,
            CourseCode = offering.course.code,
            CourseName = offering.course.name,
            Days = time?.days ?? "TBA",
            Time = time?.time ?? "TBA",
            Instructor = instructor?.instructor.name ?? "TBA",
            Location = location != null ? $"{location.building} {location.room}" : "TBA"
        };
    }
}
