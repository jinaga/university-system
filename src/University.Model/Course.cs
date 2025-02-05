namespace University.Model;

[FactType("University.Course")]
public record Course(Organization organization, string code, string name);

[FactType("University.Semester")]
public record Semester(Organization organization, int year, string term);

[FactType("University.Instructor")]
public record Instructor(Organization organization, string name);

[FactType("University.Offering")]
public record Offering(Course course, Semester semester, Guid guid)
{
    public Relation<OfferingLocation> Locations => Relation.Define<OfferingLocation>(() =>
        from location in this.Successors().OfType<OfferingLocation>(l => l.offering)
        where location.Successors().No<OfferingLocation>(next => next.prior)
        select location
    );

    public Relation<OfferingTime> Times => Relation.Define<OfferingTime>(() =>
        from time in this.Successors().OfType<OfferingTime>(t => t.offering)
        where time.Successors().No<OfferingTime>(next => next.prior)
        select time
    );

    public Relation<Instructor> Instructors => Relation.Define<Instructor>(() =>
        from offeringInstructor in this.Successors().OfType<OfferingInstructor>(oi => oi.offering)
        where offeringInstructor.Successors().No<OfferingInstructor>(next => next.prior)
        from instructor in offeringInstructor.instructor.Successors().OfType<Instructor>(i => i)
        select instructor
    );
}

[FactType("University.Offering.Location")]
public record OfferingLocation(Offering offering, string building, string room, OfferingLocation[] prior);

[FactType("University.Offering.Time")]
public record OfferingTime(Offering offering, string days, string time, OfferingTime[] prior);

[FactType("University.Offering.Instructor")]
public record OfferingInstructor(Offering offering, Instructor instructor, OfferingInstructor[] prior);

[FactType("University.Offering.Delete")]
public record OfferingDelete(Offering offering, DateTime deletedAt);
